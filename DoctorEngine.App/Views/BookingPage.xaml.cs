using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class BookingPage : ContentPage
{
    private readonly BookingViewModel _vm;
    private bool _revealed;

    public BookingPage(BookingViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_revealed) { _revealed = true; Motion.RevealSections(this); }
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "BookingPage"); }
    }
}
