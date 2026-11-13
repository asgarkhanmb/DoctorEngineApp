using DoctorEngine.App.Views;

namespace DoctorEngine.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Tablardan kənar ekranlar (yuxarıdan açılır, "geri" ilə qayıdılır)
        Routing.RegisterRoute("ai", typeof(AiPage));
    }
}
