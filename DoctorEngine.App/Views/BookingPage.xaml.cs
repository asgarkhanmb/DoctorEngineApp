using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class BookingPage : ContentPage
{
    private readonly BookingViewModel _vm;

    public BookingPage(BookingViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
