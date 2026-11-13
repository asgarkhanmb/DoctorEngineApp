using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;

    public ChatPage(ChatViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        BindingContext = _vm = vm;
        _vm.ScrollToRequested += item =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { MessagesView.ScrollTo(item, position: ScrollToPosition.End, animate: true); } catch { }
            });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearingAsync();
    }
}
