namespace DoctorEngine.App;

public partial class App : Application
{
    /// <summary>Tətbiq ekrandadırmı — arxa plandadırsa yeni bildirişlər telefonun panelinə çıxır.</summary>
    /// Android WorkManager prosesi açanda (ekran yoxdur) false qalır.
    public static bool IsInForeground { get; private set; }

    private readonly AppShell _shell;
    private readonly Services.CallCoordinator _calls;
    private readonly ViewModels.NotificationsViewModel _notifications;

    public App(AppShell shell, Services.CallCoordinator calls, ViewModels.NotificationsViewModel notifications)
    {
        InitializeComponent();
        _shell = shell;
        _calls = calls;
        _notifications = notifications;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell)
        {
            Title = "DoctorEngine",
            // Desktop: telefon kimi şaquli pəncərə (alt menyu Instagram kimidir)
            Width = DeviceInfo.Platform == DevicePlatform.WinUI ? 480 : 460,
            Height = 860,
            MinimumWidth = 360,
            MinimumHeight = 600
        };
        // Windows: pəncərə fokusda deyilsə (başqa proqrama keçilib / kiçildilib) — bildirişlər Windows bildirişi kimi çıxır
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            window.Activated += (_, _) => IsInForeground = true;
            window.Deactivated += (_, _) => IsInForeground = false;
        }
        return window;
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        IsInForeground = false;
    }

    protected override void OnResume()
    {
        base.OnResume();
        IsInForeground = true;
        _ = _notifications.RefreshCountAsync();   // tətbiq önə gələndə nişan dərhal yenilənsin
    }

    protected override void OnStart()
    {
        base.OnStart();
        IsInForeground = true;
        // Gələn zənglər (istənilən ekranda) — daxil olubsa API-nin /callHub-a qoşul
        _ = _calls.StartAsync();
        // Bildirişlər: alt menyudakı nişan hər 30 saniyədən bir yenilənir
        _notifications.StartPolling();
        // Telefonun bildiriş paneli: icazə (Android 13+) və tətbiq bağlı olanda yoxlama (WorkManager)
        _ = Services.LocalNotifier.RequestPermissionAsync();
#if ANDROID
        Platforms.Android.NotificationsWorker.Schedule();
#endif
        // Əvvəlki açılışda tətbiq xəta ilə bağlanıbsa — mətnini göstər (kopyalayıb göndərmək üçün)
        var crash = Services.CrashLog.TakeLast();
        if (crash == null) return;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1.5), async () =>
        {
            try
            {
                var shortText = crash.Length > 900 ? crash[..900] + "…" : crash;
                var copy = await _shell.DisplayAlertAsync(Localization.Lang.T("crash_title"),
                    shortText + "\n\n" + Localization.Lang.T("crash_body"), Localization.Lang.T("common_copy"), Localization.Lang.T("common_close"));
                if (copy) await Clipboard.Default.SetTextAsync(crash);
            }
            catch { }
        });
    }
}
