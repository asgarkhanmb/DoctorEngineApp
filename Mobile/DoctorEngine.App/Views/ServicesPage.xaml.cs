using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ServicesPage : ContentPage
{
    private readonly ServicesViewModel _vm;

    public ServicesPage(ServicesViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("services");   // Instagram kimi alt menyu (ikonlar)
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Motion.Enter(this);   // tab dəyişəndə məzmun yumşaq daxil olur
        try { if (_vm.Categories.Count == 0 && !_vm.IsBusy) await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ServicesPage"); }
    }
}
