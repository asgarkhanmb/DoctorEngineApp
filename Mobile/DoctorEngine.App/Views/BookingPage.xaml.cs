using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class BookingPage : ContentPage
{
    private readonly BookingViewModel _vm;
    private bool _revealed;

    public BookingPage(BookingViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("home", floating: false);   // alt menyu bütün səhifələrdə (aid olduğu bölmə seçilmiş görünür)
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (Navigation.NavigationStack.Count > 1) await Shell.Current.GoToAsync("..");
            else await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex) { Services.CrashLog.Write(ex, "Booking.Back"); }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Motion.Enter(this);   // tab dəyişəndə məzmun yumşaq daxil olur
        if (!_revealed) { _revealed = true; Motion.RevealSections(this); }
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "BookingPage"); }
    }
}
