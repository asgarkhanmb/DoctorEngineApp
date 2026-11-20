using DoctorEngine.App.Localization;
using DoctorEngine.App.Views;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>
/// Tətbiq boyu zəng idarəsi: daxil olanda /callHub-a qoşulur, gələn zəngdə tam ekran bildiriş göstərir,
/// çatdan zəng etməyə imkan verir (CallPage — WebView).
/// </summary>
public class CallCoordinator
{
    private readonly CallClient _calls;
    private readonly AuthService _auth;
    private IncomingCallPage? _ringing;

    public CallCoordinator(CallClient calls, AuthService auth)
    {
        _calls = calls;
        _auth = auth;
        _calls.IncomingCall += OnIncoming;
        _calls.CallClosed += id => { if (_ringing?.CallId == id) _ = _ringing.CloseAsync(); };
        _auth.Changed += async (_, _) =>
        {
            if (_auth.IsLoggedIn) await _calls.ConnectAsync();
            else await _calls.DisconnectAsync();
        };
    }

    public Task StartAsync() => _auth.IsLoggedIn ? _calls.ConnectAsync() : Task.CompletedTask;

    private static INavigation? Nav => Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    private async void OnIncoming(IncomingCallDto call)
    {
        try
        {
            var nav = Nav;
            if (nav == null) return;
            // Artıq zəngdəyik və ya başqa zəng çalır — yenisini göstərmə
            if (nav.ModalStack.Any(p => p is CallPage || p is IncomingCallPage)) return;
            _ringing = new IncomingCallPage(call, _calls);
            await nav.PushModalAsync(_ringing);
        }
        catch (Exception ex) { CrashLog.Write(ex, "IncomingCall"); }
    }

    /// <summary>Çatdan zəng et (yazışma və ya qrup).</summary>
    public async Task StartCallAsync(string? conversation, bool video, string? title)
    {
        if (!await CallClient.EnsurePermissionsAsync(video))
        {
            await Shell.Current.DisplayAlertAsync(Lang.T("call_title"), Lang.T("call_permission"), "OK");
            return;
        }
        // Müştərinin ustalarla yazışması — açar özü (server də qəbul edir)
        var conv = string.IsNullOrEmpty(conversation) ? _auth.User?.UserName : conversation;
        var url = await _calls.BuildCallUrlAsync(conv, null, video, title);
        var nav = Nav;
        if (nav != null) await nav.PushModalAsync(new CallPage(url));
    }
}
