using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private bool _revealed;

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Motion.Enter(this);   // tab dəyişəndə məzmun yumşaq daxil olur
        if (!_revealed) { _revealed = true; Motion.RevealSections(this); }
        try { await _vm.AppearingAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ProfilePage"); }
    }

    private void OnChatClicked(object? sender, EventArgs e) => Go("//chat");
    private void OnBookingClicked(object? sender, EventArgs e) => Go("//booking");
    private void OnAiClicked(object? sender, EventArgs e) => Go("ai");
    private void OnEditProfileClicked(object? sender, EventArgs e) => Go("profile-edit");
    private void OnAdminClicked(object? sender, EventArgs e) => Go("admin");

    private static async void Go(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { Services.CrashLog.Write(ex, "Profile→" + route); }
    }
}
