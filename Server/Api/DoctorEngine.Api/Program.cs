using System.Security.Claims;
using DoctorEngine.Api.Infrastructure;
using DoctorEngine.Contracts;
using DoctorEngineApp;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Hubs;
using DoctorEngineApp.Localization;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

// =====================================================================================
//  DoctorEngine.Api — mobil / desktop tətbiqlər üçün REST API (JWT ilə).
//  Sayt (DoctorEngineApp) ilə eyni bazanı və eyni yüklənmiş faylları (uploads) istifadə edir.
// =====================================================================================

// Yüklənmiş fayllar (uploads/...) API-nin öz wwwroot qovluğundadır; sayt (front) onları /uploads/... ilə API-dən götürür.
// "Storage:WebRoot" — standart "wwwroot" (nisbi yol API qovluğuna görə hesablanır).
var webRoot = SharedWebRoot.Resolve(args);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = webRoot });

// ---------- BAZA, IDENTITY, SERVİSLƏR, SIGNALR ----------
// AI tərcümə işçisi API-də işləyir (front bazaya qoşulmur — tərcümələri api/v1/translations-dan alır).
// Köhnə MVC sayt (DoctorEngineApp) da eyni bazada işləyirsə, işçi yalnız birində olsun: "Translations:RunWorker": false
builder.Services.AddInfrastructure(builder.Configuration,
    runTranslationWorker: builder.Configuration.GetValue("Translations:RunWorker", true));

// ---------- JWT ----------
var jwtOptions = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(opt =>
    {
        // AddIdentity cookie-ni standart edir — API-də hər şey Bearer tokenlə
        opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;   // claim adları olduğu kimi (ClaimTypes.* artıq tam URI kimidir)
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            // Açar yoxdursa — heç bir token keçərli olmur (API girişi söndürülüb)
            IssuerSigningKey = jwtOptions.IsConfigured
                ? TokenService.SigningKey(jwtOptions)
                : new SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        opt.Events = new JwtBearerEvents
        {
            // SignalR: WebSocket header göndərə bilmir — token "access_token" sorğu parametrindədir
            OnMessageReceived = ctx =>
            {
                var path = ctx.HttpContext.Request.Path;
                var token = ctx.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token) &&
                    (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/reservationHub") || path.StartsWithSegments("/callHub")))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
            // Şifrə / rol dəyişibsə (SecurityStamp fərqlidir) — köhnə token qəbul olunmur
            OnTokenValidated = async ctx =>
            {
                var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var id = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var stamp = ctx.Principal?.FindFirst("sstamp")?.Value;
                var user = id == null ? null : await users.FindByIdAsync(id);
                if (user == null || user.SecurityStamp != stamp || await users.IsLockedOutAsync(user))
                    ctx.Fail("Token artıq etibarlı deyil.");
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- RATE LIMIT ----------
// Giriş / qeydiyyat / formalarda brute-force və spam qoruması (IP üzrə dəqiqədə 20)
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
            new ApiError(L.T("Çox sayda cəhd. Bir dəqiqə gözləyib yenidən cəhd edin."), 429), ct);
    };
});

// ---------- CORS: JavaScript front (Server/Front) brauzerdən birbaşa API-yə müraciət edir ----------
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
{
    if (corsOrigins.Length > 0)
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
         // JS front oxuya bilsin: AI limiti, PDF fayl adı
         .WithExposedHeaders("X-AI-Remaining", "X-AI-Limit", "Content-Disposition");
}));

// ---------- UPLOAD LİMİTİ (çat videoları) ----------
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2_000_000_000);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 2_000_000_000);

// ---------- CONTROLLERS + OPENAPI ----------
builder.Services.AddControllersWithViews()   // view-lar yalnız zəmanət PDF-i üçün (Views/Warranty/Print.cshtml)
    .AddDataAnnotationsLocalization()   // [Required(ErrorMessage=...)] → L.T
    // Sayt (front) bazadakı modelləri olduğu kimi alır — naviqasiya dövrləri (Warranty.Items[].Warranty) kəsilir
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles)
    // Model yoxlaması uğursuzdursa — vahid xəta forması { error, code, errors }
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState.Where(kv => kv.Value!.Errors.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? L.T("Məlumatları düzgün doldurun.") : e.ErrorMessage).ToArray());
        var first = errors.Values.SelectMany(v => v).FirstOrDefault() ?? L.T("Məlumatları düzgün doldurun.");
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ApiError(first, 400, errors));
    });
builder.Services.AddOpenApi();

// ---------- LOKAL JS FRONT (yalnız Development) ----------
// API işə düşəndə Server/Front da http://localhost:5021-də açılır (ayrıca server lazım deyil).
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue("Front:Serve", true))
    builder.Services.AddHostedService<DevFrontHost>();

// ---------- SAYT (FRONT) ARXASINDA ----------
// Sayt istifadəçinin IP-sini X-Forwarded-For ilə ötürür (limitlər — AI, giriş — hər istifadəçiyə ayrıca işləsin).
// Yalnız etibarlı ünvanlardan qəbul olunur: standart — localhost (sayt və API eyni serverdədir).
// Başqa serverdədirsə: "ForwardedHeaders:KnownProxies": ["10.0.0.5"]
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    foreach (var ip in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        if (System.Net.IPAddress.TryParse(ip, out var addr)) o.KnownProxies.Add(addr);
});

var app = builder.Build();

app.UseForwardedHeaders();

L.Store = app.Services.GetRequiredService<TranslationStore>();

// Zəmanət PDF-i: wkhtmltopdf saytın wwwroot/Rotativa qovluğundadır (saytla eyni)
Rotativa.AspNetCore.RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

// Dil: tətbiqin Accept-Language başlığından (az/en/ru/tr). Ən əvvəldə — xəta cavabları da seçilmiş dildə olsun.
app.Use(async (ctx, next) =>
{
    L.Apply(ctx);
    await next();
});

// ---------- XƏTALAR → JSON ----------
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var error = ctx.Features.Get<IExceptionHandlerPathFeature>()?.Error;
    var code = error is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError;
    if (error != null)
        ctx.RequestServices.GetRequiredService<ILogger<Program>>()
            .LogError(error, "Tutulmamış xəta: {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

    ctx.Response.StatusCode = code;
    ctx.Response.ContentType = "application/json; charset=utf-8";
    await ctx.Response.WriteAsJsonAsync(new ApiError(ErrorPages.For(code).Message, code));
}));
// Boş cavablı 4xx/5xx (404, 401, 403 ...) → { error, code }
app.UseStatusCodePages(async ctx =>
{
    var res = ctx.HttpContext.Response;
    res.ContentType = "application/json; charset=utf-8";
    await res.WriteAsJsonAsync(new ApiError(ErrorPages.For(res.StatusCode).Message, res.StatusCode));
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    // Development-də HTTPS-ə yönləndirmə yoxdur: emulyator/telefon lokal serverə HTTP ilə qoşulur
    app.UseHttpsRedirection();
}

// ---------- YÜKLƏNMİŞ FAYLLAR: /uploads/... (saytla ortaq qovluq) ----------
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        // İstifadəçilərin yüklədiyi çat faylları heç vaxt skript kimi işləməsin
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads/chat"))
            ctx.Context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'";
    }
});

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ---------- OPENAPI / SWAGGER ----------
// Production-da söndürülüb; açmaq üçün: "OpenApi:Enabled": true
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();                                   // /openapi/v1.json
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "DoctorEngine API v1");
        o.RoutePrefix = "swagger";                      // /swagger
    });
}

app.MapGet("/", () => Results.Ok(new { name = "DoctorEngine API", status = "ok" })).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapHub<ReservationHub>("/reservationHub");   // rezervasiyalar: boş saatlar, status, admin bildirişləri
app.MapHub<CallHub>("/callHub");                 // video / səsli zənglər (WebRTC siqnalizasiyası)

app.Run();
