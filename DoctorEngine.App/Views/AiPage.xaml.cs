using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class AiPage : ContentPage
{
    public AiPage(AiViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = vm;
        Motion.Pulse(Orb, 0.75, 1800);

        // Windows: CollectionView.ScrollTo yeni əlavə olunmuş elementə dərhal çağırılanda WinUI çökür;
        // orada ItemsUpdatingScrollMode=KeepLastItemInView sürüşdürməni özü edir.
        if (DeviceInfo.Platform == DevicePlatform.WinUI) return;
        vm.ScrollToRequested += item =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { MessagesView.ScrollTo(item, position: ScrollToPosition.End, animate: false); } catch { }
            });
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { Services.CrashLog.Write(ex, "AiPage.Back"); }
    }
}
