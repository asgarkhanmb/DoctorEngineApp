using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _vm;

    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("notifications");   // Instagram kimi alt menyu
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "NotificationsPage"); }
    }
}
