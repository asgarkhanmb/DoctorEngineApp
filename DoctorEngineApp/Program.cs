using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Hubs;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Implementations;
using DoctorEngineApp.Services.Interfaces;
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
    opt.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    opt.Filters.Add<DoctorEngineApp.Helpers.AdminFlashFilter>();   // admin əməliyyatlarından sonra bildiriş
});

var app = builder.Build();

// ROTATIVA
RotativaConfiguration.Setup(
    app.Environment.WebRootPath,
    "Rotativa"
);

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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