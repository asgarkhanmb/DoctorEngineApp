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
}
