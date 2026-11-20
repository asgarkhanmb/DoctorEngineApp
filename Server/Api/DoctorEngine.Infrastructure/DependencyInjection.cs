using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Localization;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Implementations;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoctorEngineApp
{
    /// <summary>
    /// Sayt (DoctorEngineApp) və API (DoctorEngine.Api) üçün ortaq qeydiyyatlar:
    /// baza, Identity, biznes servisləri, ayarlar, SignalR, Data Protection, tərcümə.
    /// Hər iki tətbiq eyni bazaya qoşulur — biri ilə edilən dəyişiklik o birində dərhal görünür.
    /// </summary>
    public static class DependencyInjection
    {
        /// <param name="runTranslationWorker">
        /// true — AI tərcümə işçisi bu prosesdə işləyir (yalnız BİR tətbiqdə: sayt).
        /// false — tərcümələr yalnız bazadan oxunur və vaxtaşırı yenilənir (API).
        /// </param>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config,
                                                           bool runTranslationWorker)
        {
            // ---------- BAZA ----------
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(config.GetConnectionString("Default")));

            // ---------- IDENTITY ----------
            services.AddIdentity<AppUser, IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders()
                .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()   // profil şəkli cookie-də
                .AddErrorDescriber<AzIdentityErrorDescriber>();           // Identity xətaları Azərbaycan dilində

            services.Configure<IdentityOptions>(opt =>
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

            // ---------- DATA PROTECTION ----------
            // Email təsdiq / şifrə bərpası tokenləri API-də yaradılıb saytda yoxlanılır (və əksinə) —
            // ona görə hər iki tətbiq EYNİ açar qovluğunu və tətbiq adını işlətməlidir.
            var keysPath = config["DataProtection:KeysPath"];
            if (string.IsNullOrWhiteSpace(keysPath))
                keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoctorEngine", "keys");
            services.AddDataProtection()
                .SetApplicationName("DoctorEngine")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

            // ---------- AYARLAR ----------
            services.Configure<EmailSettings>(config.GetSection("EmailSettings"));
            services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
            services.Configure<PushOptions>(config.GetSection(PushOptions.Section));
            services.Configure<AiOptions>(config.GetSection(AiOptions.Section));   // API açarı: dotnet user-secrets set "Ai:ApiKey" "<açar>"

            // ---------- SERVİSLƏR ----------
            services.AddScoped<ISliderService, SliderService>();
            services.AddScoped<IInformService, InformService>();
            services.AddScoped<IAboutService, AboutService>();
            services.AddScoped<ISubServiceService, SubServiceService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IReservationService, ReservationService>();
            services.AddScoped<IServiceService, ServiceService>();
            services.AddScoped<ITechnicianService, TechnicianService>();
            services.AddScoped<IContactInfoService, ContactInfoService>();
            services.AddScoped<IContactPhoneService, ContactPhoneService>();
            services.AddScoped<IMapService, MapService>();
            services.AddScoped<IContactMessageService, ContactMessageService>();
            services.AddScoped<IWarrantyService, WarrantyService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IWhatsappService, WhatsappService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChatNotifier, ChatNotifier>();                // çat hadisələri (SignalR)
            services.AddScoped<ISiteSettingService, SiteSettingService>();   // sayt adı və loqo
            services.AddScoped<PushService>();                               // Web Push: yeni mesaj bildirişi
            services.AddScoped<TokenService>();                              // tətbiqlər üçün JWT

            // AI köməkçi (OpenAI-uyğun API; standart: Google Gemini)
            services.AddMemoryCache();
            services.AddHttpClient<AiAssistantService>(client => client.Timeout = TimeSpan.FromSeconds(90));

            // ---------- ÇOXDİLLİLİK (az/en/ru/tr) ----------
            services.AddSingleton<TranslationStore>();
            if (runTranslationWorker)
            {
                services.AddHostedService<ContentTranslationWorker>();
                services.AddHttpClient("ai-translate", client => client.Timeout = TimeSpan.FromSeconds(120));
            }
            else
            {
                services.AddHostedService<TranslationReloadWorker>();
            }
            // Validasiya mesajları və [Display] adları da L.T ilə tərcümə olunur
            services.AddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory, LStringLocalizerFactory>();

            // ---------- SIGNALR ----------
            // Sayt və API ayrı proseslərdir: birində göndərilən canlı hadisə (yeni mesaj, rezervasiya)
            // o birinə qoşulanlara çatsın deyə Redis "backplane" lazımdır. ConnectionStrings:Redis boşdursa —
            // hər tətbiq yalnız öz qoşulanlarına göndərir (səhifəni yeniləyəndə məlumat yenə görünür).
            var signalR = services.AddSignalR();
            var redis = config.GetConnectionString("Redis");
            if (!string.IsNullOrWhiteSpace(redis))
                signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix =
                    StackExchange.Redis.RedisChannel.Literal("doctorengine"));

            services.AddHttpContextAccessor();
            return services;
        }
    }
}
