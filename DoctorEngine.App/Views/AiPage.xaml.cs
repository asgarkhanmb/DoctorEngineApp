using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class AiPage : ContentPage
{
    public AiPage(AiViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = vm;
        // Windows: CollectionView.ScrollTo yeni əlavə olunmuş elementə dərhal çağırılanda WinUI çökür;
        // orada ItemsUpdatingScrollMode=KeepLastItemInView sürüşdürməni özü edir.
        if (DeviceInfo.Platform == DevicePlatform.WinUI) return;
        vm.ScrollToRequested += item =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { MessagesView.ScrollTo(item, position: ScrollToPosition.End, animate: false); } catch { }
            });
    }
}
