using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ServicesPage : ContentPage
{
    private readonly ServicesViewModel _vm;

    public ServicesPage(ServicesViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Categories.Count == 0 && !_vm.IsBusy) await _vm.LoadAsync();
    }
}
