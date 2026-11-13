using DoctorEngine.App.Services;
using DoctorEngine.App.ViewModels;
using DoctorEngine.App.Views;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;

namespace DoctorEngine.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .AddAudio();   // Plugin.Maui.Audio — səs mesajı yazma / oxutma

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Xidmətlər (tətbiq boyu bir nüsxə — sessiya, çat bağlantısı)
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ContentService>();
        builder.Services.AddSingleton<BookingService>();
        builder.Services.AddSingleton<ChatClient>();
        builder.Services.AddSingleton<AiClient>();
        builder.Services.AddSingleton<IVoiceRecorder, VoiceRecorder>();
        builder.Services.AddSingleton<IAudioPlayback, AudioPlayback>();

        // Ekranlar və onların məntiqi (tablar yaddaşda qalır)
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<ServicesViewModel>();
        builder.Services.AddSingleton<BookingViewModel>();
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddTransient<AiViewModel>();

        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<ServicesPage>();
        builder.Services.AddSingleton<BookingPage>();
        builder.Services.AddSingleton<ChatPage>();
        builder.Services.AddSingleton<ProfilePage>();
        builder.Services.AddTransient<AiPage>();
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();

        // Saxlanmış sessiyanı yüklə (SecureStorage) — ilk ekran açılmazdan əvvəl
        // Task.Run vacibdir: UI axınında birbaşa gözləmək (GetResult) SecureStorage-in davamını
        // UI axınına qaytarmağa çalışdığı üçün kilidlənmə yaradır (tətbiq pəncərəsiz qalır).
        var auth = app.Services.GetRequiredService<AuthService>();
        Task.Run(auth.InitializeAsync).GetAwaiter().GetResult();

        return app;
    }
}
