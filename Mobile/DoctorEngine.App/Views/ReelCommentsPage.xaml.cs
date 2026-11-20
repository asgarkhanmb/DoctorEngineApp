using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ReelCommentsPage : ContentPage
{
    private readonly ReelCommentsViewModel _vm;
    private bool _loaded;

    public ReelCommentsPage(ReelCommentsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.FocusInput += (_, _) => MainThread.BeginInvokeOnMainThread(() => Input.Focus());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        Sheet.TranslationY = 500;
        _ = Sheet.TranslateToAsync(0, 0, 260, Easing.CubicOut);
        try { await _vm.LoadAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ReelComments"); }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ReelsPage.KeepPlayingInBackground = false;
    }
}
