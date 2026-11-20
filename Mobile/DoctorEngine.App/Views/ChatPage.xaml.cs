using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;

    public ChatPage(ChatViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("chat", floating: false);   // Instagram kimi alt menyu; yazı sahəsi örtülməsin
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;
        Motion.Pulse(RecDot, 0.25, 800);
        _vm.ScrollToRequested += item =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { MessagesView.ScrollTo(item, position: ScrollToPosition.Center, animate: true); } catch { }
            });
        _vm.PropertyChanged += (_, e) =>
        {
            // Axtarış açılanda klaviatura dərhal açılsın
            if (e.PropertyName == nameof(ChatViewModel.IsSearching) && _vm.IsSearching)
                MainThread.BeginInvokeOnMainThread(() => SearchEntry.Focus());
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Motion.Enter(this);   // tab dəyişəndə məzmun yumşaq daxil olur
        try { await _vm.AppearingAsync(); } catch (Exception ex) { Services.CrashLog.Write(ex, "ChatPage"); }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Disappearing();
    }

    /// <summary>Android "geri": axtarış / cavab / usta yazışması açıqdırsa — əvvəl onu bağla.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsSearching) { _vm.ToggleSearchCommand.Execute(null); return true; }
        if (_vm.ShowEmoji) { _vm.ShowEmoji = false; return true; }
        if (_vm.IsReplying) { _vm.CancelReplyCommand.Execute(null); return true; }
        if (_vm.IsAdmin && !_vm.IsListMode) { _vm.BackToListCommand.Execute(null); return true; }
        return base.OnBackButtonPressed();
    }
}
