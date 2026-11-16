namespace DoctorEngine.App;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell)
        {
            Title = "DoctorEngine",
            // Desktop: Windows-da tablar yuxarıda düzülür — 5 tabın hamısı sığsın deyə bir az enli
            Width = DeviceInfo.Platform == DevicePlatform.WinUI ? 800 : 460,
            Height = 860,
            MinimumWidth = 360,
            MinimumHeight = 600
        };

    protected override void OnStart()
    {
        base.OnStart();
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
