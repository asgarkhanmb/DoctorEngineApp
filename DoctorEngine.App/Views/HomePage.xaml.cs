using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Slides.Count == 0 && !_vm.IsBusy) await _vm.LoadAsync();
    }

    private async void OnChatClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//chat");

    private async void OnAiClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("ai");
}
