using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Hubs;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Implementations;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Connection String
var connectionString = builder.Configuration.GetConnectionString("Default");

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()   // profil şəkli cookie-də
    .AddErrorDescriber<AzIdentityErrorDescriber>();   // Identity xətaları Azərbaycan dilində

builder.Services.Configure<IdentityOptions>(opt =>
{
    opt.Password.RequiredUniqueChars = 1;
    opt.Password.RequireNonAlphanumeric = true;
    opt.Password.RequireDigit = true;
    opt.Password.RequireLowercase = true;
    opt.Password.RequireUppercase = true;
    opt.User.RequireUniqueEmail = true;

    // Email təsdiqlənmədən giriş qadağandır
    opt.SignIn.RequireConfirmedEmail = true;

    // Brute-force qoruması: 5 uğursuz cəhddən sonra 15 dəqiqəlik blok
    opt.Lockout.AllowedForNewUsers = true;
    opt.Lockout.MaxFailedAccessAttempts = 5;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/Account/Login";
    opt.AccessDeniedPath = "/Account/Login";
    opt.Cookie.HttpOnly = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    // API sorğuları üçün login səhifəsinə yönləndirmə əvəzinə 401/403 qaytar
    opt.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/chatHub"))
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    opt.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// ---------- TƏTBİQLƏR ÜÇÜN JWT ----------
// Sayt cookie ilə, mobil/desktop tətbiqlər "Authorization: Bearer <token>" ilə daxil olur.
// "Smart" sxemi sorğuya görə birini seçir — mövcud [Authorize] atributları hər ikisi ilə işləyir.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
const string SmartScheme = "Smart";

builder.Services.AddAuthentication(opt =>
    {
        opt.DefaultScheme = SmartScheme;
        opt.DefaultAuthenticateScheme = SmartScheme;
        opt.DefaultChallengeScheme = SmartScheme;
        opt.DefaultForbidScheme = SmartScheme;
    })
    .AddPolicyScheme(SmartScheme, "Cookie və ya JWT", opt =>
    {
        opt.ForwardDefaultSelector = ctx =>
        {
            if (SmartAntiforgeryFilter.IsBearer(ctx)) return JwtBearerDefaults.AuthenticationScheme;
            // SignalR: WebSocket header göndərə bilmir — token "access_token" sorğu parametrindədir
            if (ctx.Request.Query.ContainsKey("access_token") &&
                (ctx.Request.Path.StartsWithSegments("/chatHub") || ctx.Request.Path.StartsWithSegments("/reservationHub")))
                return JwtBearerDefaults.AuthenticationScheme;
            return IdentityConstants.ApplicationScheme;
        };
    })
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;   // claim adları olduğu kimi (ClaimTypes.* artıq tam URI kimidir)
        opt.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            // Açar yoxdursa — heç bir token keçərli olmur (API girişi söndürülüb)
            IssuerSigningKey = jwtOptions.IsConfigured
                ? TokenService.SigningKey(jwtOptions)
                : new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var path = ctx.HttpContext.Request.Path;
                var token = ctx.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token) &&
                    (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/reservationHub")))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
            // Şifrə / rol dəyişibsə (SecurityStamp fərqlidir) — köhnə token qəbul olunmur
            OnTokenValidated = async ctx =>
            {
                var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var id = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var stamp = ctx.Principal?.FindFirst("sstamp")?.Value;
                var user = id == null ? null : await users.FindByIdAsync(id);
                if (user == null || user.SecurityStamp != stamp || await users.IsLockedOutAsync(user))
                    ctx.Fail("Token artıq etibarlı deyil.");
            }
        };
    });

builder.Services.AddScoped<TokenService>();

// Giriş / qeydiyyat / formalarda brute-force və spam qoruması (Identity lockout-a əlavə olaraq, IP üzrə).
// 20/dəq: mobil operatorlarda yüzlərlə istifadəçi eyni IP-ni paylaşa bilər (CGNAT) — daha sərt limit real istifadəçiləri bloklayardı.
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("auth", ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    opt.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new DoctorEngine.Contracts.ApiError("Çox sayda cəhd. Bir dəqiqə gözləyib yenidən cəhd edin.", 429), ct);
    };
});

// EMAIL SETTINGS
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// SERVICES
builder.Services.AddScoped<ISliderService, SliderService>();
builder.Services.AddScoped<IInformService, InformService>();
builder.Services.AddScoped<IAboutService, AboutService>();
builder.Services.AddScoped<ISubServiceService, SubServiceService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ITechnicianService, TechnicianService>();
builder.Services.AddScoped<IContactInfoService, ContactInfoService>();
builder.Services.AddScoped<IContactPhoneService, ContactPhoneService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<IContactMessageService, ContactMessageService>();
builder.Services.AddScoped<IWarrantyService, WarrantyService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IWhatsappService, WhatsappService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<PushService>();   // Web Push: sayt bağlı olanda da yeni mesaj bildirişi
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.Section));
builder.Services.AddScoped<ISiteSettingService, SiteSettingService>();   // sayt adı və loqo

// AI KÖMƏKÇİ (OpenAI-uyğun API; standart: Google Gemini pulsuz versiyası)
// API açarı: dotnet user-secrets set "Ai:ApiKey" "<açar>"
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<AiAssistantService>(client => client.Timeout = TimeSpan.FromSeconds(90));

builder.Services.AddSignalR();

// Upload limit
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 2_000_000_000;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2_000_000_000;
});

// HttpContext
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Antiforgery: AJAX sorğuları tokeni bu header ilə göndərir
builder.Services.AddAntiforgery(opt => opt.HeaderName = "RequestVerificationToken");

// MVC — bütün POST/PUT/DELETE sorğularında antiforgery token avtomatik yoxlanılır (CSRF qoruması)
builder.Services.AddControllersWithViews(opt =>
    {
    // Sayt (cookie) sorğularında CSRF yoxlanılır; tətbiqlərin Bearer sorğularında lazım deyil (bax: SmartAntiforgeryFilter)
    opt.Filters.Add<SmartAntiforgeryFilter>();
    opt.Filters.Add<DoctorEngineApp.Helpers.AdminFlashFilter>();   // admin əməliyyatlarından sonra bildiriş
    opt.Filters.Add<DoctorEngineApp.Helpers.AdminExceptionFilter>(); // admin xətaları → qırmızı bildiriş, xəta səhifəsi yox
});

var app = builder.Build();

// ROTATIVA
RotativaConfiguration.Setup(
    app.Environment.WebRootPath,
    "Rotativa"
);

// ---------- XƏTALAR ----------
// Tutulmamış exception → /Error (ErrorController: loga yazır, istifadəçiyə səliqəli səhifə, API-yə JSON).
// Development-də proqramçı üçün ətraflı səhifə qalır; nəticəni görmək üçün: /Error/500, /Error/404 ...
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Boş cavablı 4xx/5xx (404 tapılmadı, 403 qadağan ...):
//  - səhifələr → /Error/{kod} (eyni ünvanda, yönləndirmə olmadan)
//  - API / hub-lar → qısa JSON { error, code }
app.UseWhen(ctx => !ErrorPages.IsNonPageRequest(ctx.Request.Path),
    branch => branch.UseStatusCodePagesWithReExecute("/Error/{0}"));
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePages(async ctx =>
    {
        var res = ctx.HttpContext.Response;
        res.ContentType = "application/json; charset=utf-8";
        await res.WriteAsJsonAsync(new { error = ErrorPages.For(res.StatusCode).Message, code = res.StatusCode });
    }));

// Development-də HTTP → HTTPS yönləndirmə yoxdur: mobil tətbiq (Android emulyatoru 10.0.2.2, telefon LAN IP)
// lokal serverə HTTP ilə qoşulur və "localhost:7192"-yə yönləndirmə ona çatmır. Production-da həmişə HTTPS.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Brauzer faylın tipini "təxmin etməsin" (yüklənmiş faylın HTML/JS kimi işlənməsinin qarşısı)
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // İstifadəçilərin yüklədiyi çat faylları heç vaxt skript kimi işləməsin
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads/chat"))
            ctx.Context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'";
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();   // api/v1/auth: IP üzrə limit

app.MapHub<ChatHub>("/chatHub");
app.MapHub<ReservationHub>("/reservationHub");   // rezervasiyalar: boş saatlar, status, admin bildirişləri

// Area routing
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");   // /Admin → Dashboard

// Default routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();