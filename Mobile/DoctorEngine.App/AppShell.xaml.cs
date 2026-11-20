using DoctorEngine.App.Views;

namespace DoctorEngine.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Tablardan kənar ekranlar (yuxarıdan açılır, "geri" ilə qayıdılır)
        Routing.RegisterRoute("ai", typeof(AiPage));
        Routing.RegisterRoute("profile-edit", typeof(EditProfilePage));
        Routing.RegisterRoute("chat-group", typeof(ChatGroupPage));
        Routing.RegisterRoute("chat-new", typeof(ChatNewPage));       // yeni çat: istifadəçi seç   // usta: qrup yarat / redaktə
        // Mobil admin panel (yalnız Admin rolu — server də yoxlayır)
        Routing.RegisterRoute("admin", typeof(AdminPage));
        Routing.RegisterRoute("admin-reservations", typeof(AdminReservationsPage));
        Routing.RegisterRoute("admin-messages", typeof(AdminMessagesPage));
        Routing.RegisterRoute("admin-users", typeof(AdminUsersPage));
        Routing.RegisterRoute("admin-section", typeof(AdminSectionPage));
        Routing.RegisterRoute("admin-form", typeof(AdminFormPage));
    }
}
