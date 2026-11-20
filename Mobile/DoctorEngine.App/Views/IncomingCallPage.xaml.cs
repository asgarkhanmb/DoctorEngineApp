using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Views;

/// <summary>Gələn zəng ekranı. Cavab → CallPage (WebView), rədd → serverə Decline.</summary>
public partial class IncomingCallPage : ContentPage
{
    private readonly IncomingCallDto _call;
    private readonly CallClient _calls;
    private bool _done;
    private CancellationTokenSource? _vibration;

    public string CallId => _call.CallId;

    public IncomingCallPage(IncomingCallDto call, CallClient calls)
    {
        InitializeComponent();
        _call = call;
        _calls = calls;
        var title = string.IsNullOrWhiteSpace(call.Title) ? call.FromName : call.Title;
        TitleLabel.Text = title;
        Initial.Text = string.IsNullOrWhiteSpace(title) ? "?" : title.Trim()[..1].ToUpperInvariant();
        SubtitleLabel.Text = call.IsGroup
            ? Lang.T("call_group_started", call.FromName)
            : Lang.T(call.Video ? "call_incoming_video" : "call_incoming_audio");
        AcceptIcon.Source = call.Video ? "ic_video_w.png" : "ic_phone_w.png";
        Motion.Pulse(Pulse, 0.35, 900);
        StartVibration();
        // 45 saniyə cavab verilməsə — bağlanır
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(45), () => _ = CloseAsync());
    }

    private void StartVibration()
    {
        _vibration = new CancellationTokenSource();
        var token = _vibration.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(600)); } catch { return; }
                try { await Task.Delay(1800, token); } catch { return; }
            }
        });
    }

    public async Task CloseAsync()
    {
        if (_done) return;
        _done = true;
        _vibration?.Cancel();
        try { Vibration.Default.Cancel(); } catch { }
        if (Navigation.ModalStack.Contains(this)) await Navigation.PopModalAsync();
    }

    private async void OnDecline(object? sender, TappedEventArgs e)
    {
        await _calls.DeclineAsync(_call.CallId);
        await CloseAsync();
    }

    private async void OnAccept(object? sender, TappedEventArgs e)
    {
        if (_done) return;
        if (!await CallClient.EnsurePermissionsAsync(_call.Video))
        {
            await DisplayAlertAsync(Lang.T("call_title"), Lang.T("call_permission"), "OK");
            return;
        }
        var url = await _calls.BuildCallUrlAsync(null, _call.CallId, _call.Video, _call.Title);
        var nav = Navigation;
        await CloseAsync();
        await nav.PushModalAsync(new CallPage(url));
    }
}
