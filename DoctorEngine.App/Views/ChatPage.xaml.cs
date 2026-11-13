using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;

    public ChatPage(ChatViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
        Motion.Pulse(OnlineDot, 0.5, 1400);
        Motion.Pulse(RecDot, 0.25, 800);
        _vm.ScrollToRequested += item =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { MessagesView.ScrollTo(item, position: ScrollToPosition.End, animate: true); } catch { }
            });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.AppearingAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ChatPage"); }
    }
}
