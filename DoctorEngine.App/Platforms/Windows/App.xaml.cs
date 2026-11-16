using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoctorEngine.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
		// WinUI-də UI axınındakı tutulmamış xəta (0xC000027B) — jurnala yazılır
		this.UnhandledException += (_, e) => DoctorEngine.App.Services.CrashLog.Write(e.Exception, "WinUI");
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

