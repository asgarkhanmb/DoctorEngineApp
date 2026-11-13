using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.AppearingAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ProfilePage"); }
    }

    private async void OnChatClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//chat");
    private async void OnBookingClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//booking");
    private async void OnAiClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("ai");
}
