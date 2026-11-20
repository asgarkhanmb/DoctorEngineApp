using DoctorEngine.App.Services;
using DoctorEngine.App.ViewModels;
using DoctorEngine.App.Views;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;

namespace DoctorEngine.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CrashLog.Install();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .AddAudio()   // Plugin.Maui.Audio — səs mesajı yazma / oxutma
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)   // Videolar (Reels) — video oynadıcı
            .ConfigureFonts(fonts =>
            {
                // Saytın şrifti — Barlow (bütün Azərbaycan hərfləri: ə, ğ, ı, İ, ş, ç, ö, ü)
                fonts.AddFont("Barlow-Regular.ttf", "Barlow");
                fonts.AddFont("Barlow-Medium.ttf", "BarlowMedium");
                fonts.AddFont("Barlow-SemiBold.ttf", "BarlowSemiBold");
                fonts.AddFont("Barlow-Bold.ttf", "BarlowBold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                // Alt menyuda bütün tabların yazısı görünsün
                handlers.AddHandler(typeof(Shell), typeof(Platforms.Android.DeShellRenderer));
#endif
            });

        RemoveNativeFieldChrome();

        // Bütün düymələr basanda yayla sıxılır ("canlı" toxunuş)
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("DePress", (_, view) =>
        {
            if (view is Button b) Motion.AttachButtonPress(b);
        });

        // Zəng səhifəsi (WebRTC) WebView-da: kamera / mikrofon icazəsi, avtomatik səs
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("DeCalls", (handler, _) =>
        {
#if ANDROID
            var wv = handler.PlatformView;
            wv.Settings.JavaScriptEnabled = true;
            wv.Settings.DomStorageEnabled = true;
            wv.Settings.MediaPlaybackRequiresUserGesture = false;
            if (handler is Microsoft.Maui.Handlers.WebViewHandler wvh) wv.SetWebChromeClient(new Platforms.Android.CallChromeClient(wvh));
#if TESTBUILD
            if (handler is Microsoft.Maui.Handlers.WebViewHandler wvh2) wv.SetWebViewClient(new Platforms.Android.TestSslWebViewClient(wvh2));
#endif
#elif WINDOWS
            var wv2 = handler.PlatformView;
            wv2.CoreWebView2Initialized += (s, _) =>
            {
                s.CoreWebView2.PermissionRequested += (_, e) =>
                {
                    if (e.PermissionKind is Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Camera
                        or Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Microphone)
                        e.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Allow;
                };
#if TESTBUILD
                s.CoreWebView2.ServerCertificateErrorDetected += (_, e) =>
                    e.Action = Microsoft.Web.WebView2.Core.CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
#endif
            };
#endif
        });

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
        builder.Services.AddSingleton<CallClient>();   // video / səsli zənglər (API-nin /callHub)
        builder.Services.AddSingleton<CallCoordinator>();
        builder.Services.AddSingleton<AiClient>();
        builder.Services.AddSingleton<IVoiceRecorder, VoiceRecorder>();
        builder.Services.AddSingleton<IAudioPlayback, AudioPlayback>();
        builder.Services.AddSingleton<AdminClient>();
        builder.Services.AddSingleton<ReelsService>();

        // Ekranlar və onların məntiqi (tablar yaddaşda qalır)
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<ServicesViewModel>();
        builder.Services.AddSingleton<BookingViewModel>();
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddSingleton<ReelsViewModel>();
        builder.Services.AddSingleton<NotificationsViewModel>();   // bildirişlər + alt menyudakı nişan
        builder.Services.AddTransient<AiViewModel>();

        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<ServicesPage>();
        builder.Services.AddTransient<BookingPage>();   // artıq tab deyil — "booking" marşrutu ilə açılır (VM tək nüsxədir, seçimlər qalır)
        builder.Services.AddSingleton<ChatPage>();
        builder.Services.AddSingleton<ProfilePage>();
        builder.Services.AddSingleton<ReelsPage>();
        builder.Services.AddSingleton<NotificationsPage>();
        builder.Services.AddTransient<AiPage>();

        // Profil redaktəsi və mobil admin panel — hər açılışda yeni nüsxə
        builder.Services.AddTransient<ChatGroupViewModel>();
        builder.Services.AddTransient<ChatGroupPage>();
        builder.Services.AddTransient<ChatNewViewModel>();
        builder.Services.AddTransient<ChatNewPage>();
        builder.Services.AddTransient<EditProfileViewModel>();
        builder.Services.AddTransient<EditProfilePage>();
        builder.Services.AddTransient<AdminViewModel>();
        builder.Services.AddTransient<AdminPage>();
        builder.Services.AddTransient<AdminReservationsViewModel>();
        builder.Services.AddTransient<AdminReservationsPage>();
        builder.Services.AddTransient<AdminMessagesViewModel>();
        builder.Services.AddTransient<AdminMessagesPage>();
        builder.Services.AddTransient<AdminUsersViewModel>();
        builder.Services.AddTransient<AdminUsersPage>();
        builder.Services.AddTransient<AdminSectionViewModel>();
        builder.Services.AddTransient<AdminSectionPage>();
        builder.Services.AddTransient<AdminFormViewModel>();
        builder.Services.AddTransient<AdminFormPage>();
        builder.Services.AddTransient<ReelCommentsViewModel>();
        builder.Services.AddTransient<ReelCommentsPage>();
        builder.Services.AddTransient<ReelNewViewModel>();
        builder.Services.AddTransient<ReelNewPage>();
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();

        // Saxlanmış sessiyanı yüklə (SecureStorage) — ilk ekran açılmazdan əvvəl
        // Task.Run vacibdir: UI axınında birbaşa gözləmək (GetResult) SecureStorage-in davamını
        // UI axınına qaytarmağa çalışdığı üçün kilidlənmə yaradır (tətbiq pəncərəsiz qalır).
        var auth = app.Services.GetRequiredService<AuthService>();
        Task.Run(auth.InitializeAsync).GetAwaiter().GetResult();

        return app;
    }

    /// <summary>
    /// Sahələr (Entry, Editor, Picker) öz haşiyəli qabımızın (Style "Field") içindədir — platformanın
    /// öz alt xətti / çərçivəsi ikiqat görünür. Android-də alt xətt, Windows-da çərçivə və fon götürülür.
    /// </summary>
    private static void RemoveNativeFieldChrome()
    {
#if ANDROID
        static void NoUnderline(global::Android.Views.View v)
        {
            v.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(global::Android.Graphics.Color.Transparent);
            v.SetPadding(0, v.PaddingTop, 0, v.PaddingBottom);
        }
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("DeNoUnderline", (h, _) => NoUnderline(h.PlatformView));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("DeNoUnderline", (h, _) => NoUnderline(h.PlatformView));
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("DeNoUnderline", (h, _) => NoUnderline(h.PlatformView));
#elif WINDOWS
        // Diqqət: WinUI ResourceDictionary-yə eyni açarı təkrar yazmaq (mapper bir neçə dəfə işləyir və
        // MAUI özü də bu açarları qoyur) 0x800F0902 xətası atır və tətbiqi bağlayır — ona görə ehtiyatlı yazılır.
        static void SetRes(Microsoft.UI.Xaml.ResourceDictionary r, string key, object value)
        {
            try
            {
                if (r.ContainsKey(key)) r.Remove(key);
                r.Add(key, value);
            }
            catch { /* bəzək üçündür — alınmasa standart görünüş qalır */ }
        }
        static void NoBorder(Microsoft.UI.Xaml.Controls.Control c)
        {
            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            c.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            c.Background = transparent;
            SetRes(c.Resources, "TextControlBorderThemeThicknessFocused", new Microsoft.UI.Xaml.Thickness(0));
            SetRes(c.Resources, "TextControlBackgroundFocused", transparent);
            SetRes(c.Resources, "TextControlBackgroundPointerOver", transparent);
        }
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("DeNoBorder", (h, _) => NoBorder(h.PlatformView));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("DeNoBorder", (h, _) => NoBorder(h.PlatformView));
#endif
    }
}
