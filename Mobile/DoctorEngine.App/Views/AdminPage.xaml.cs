using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class AdminPage : ContentPage
{
    private readonly AdminViewModel _vm;
    private bool _revealed;

    public AdminPage(AdminViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    /// <summary>Hər qayıdışda statistika yenilənir (məs. rezervasiya təsdiqləndikdən sonra).</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_revealed) { _revealed = true; Motion.RevealSections(this); }
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "AdminPage"); }
    }
}
