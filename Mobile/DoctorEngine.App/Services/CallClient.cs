using DoctorEngine.App.Localization;
using DoctorEngine.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace DoctorEngine.App.Services;

/// <summary>
/// Video / səsli zənglər. Siqnalizasiya API-dədir (AppConfig.BaseUrl + "/callHub") — brauzer (JS front) və tətbiq eyni hub-a qoşulur.
/// Zəngin özü (kamera, WebRTC) WebView-da frontun /Call səhifəsində gedir (Views/CallPage).
/// </summary>
public class CallClient
{
    private readonly ApiClient _api;
    private HubConnection? _hub;

    public event Action<IncomingCallDto>? IncomingCall;
    /// <summary>Zəng başqa yerdə cavablandı və ya ləğv olundu (callId).</summary>
    public event Action<string>? CallClosed;

    public CallClient(ApiClient api) => _api = api;

    public async Task ConnectAsync()
    {
        if (_hub != null) return;
        _hub = new HubConnectionBuilder()
            .WithUrl(AppConfig.BaseUrl + "/callHub", o =>
            {
                o.AccessTokenProvider = () => _api.GetValidAccessTokenAsync();
#if TESTBUILD
                // Test APK: kompüterdəki saytın lokal HTTPS sertifikatı etibarlı deyil — qəbul edilir (yalnız test qurulmasında)
                o.HttpMessageHandlerFactory = h =>
                {
                    if (h is HttpClientHandler hh) hh.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                    return h;
                };
                o.WebSocketConfiguration = ws => ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#endif
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30) })
            .Build();

        _hub.On<IncomingCallDto>("IncomingCall", c => MainThread.BeginInvokeOnMainThread(() => IncomingCall?.Invoke(c)));
        _hub.On<CallRefDto>("CallAnswered", c => MainThread.BeginInvokeOnMainThread(() => CallClosed?.Invoke(c.CallId)));
        _hub.On<CallRefDto>("CallCancelled", c => MainThread.BeginInvokeOnMainThread(() => CallClosed?.Invoke(c.CallId)));
        _hub.Closed += async _ =>
        {
            await Task.Delay(8000);
            await StartSafeAsync();
        };
        await StartSafeAsync();
    }

    private async Task StartSafeAsync()
    {
        if (_hub == null || _hub.State != HubConnectionState.Disconnected) return;
        try { await _hub.StartAsync(); }
        catch { /* sayt əlçatan deyil — növbəti cəhd Closed-da */ }
    }

    public async Task DisconnectAsync()
    {
        if (_hub == null) return;
        var hub = _hub;
        _hub = null;
        try { await hub.DisposeAsync(); } catch { }
    }

    public async Task DeclineAsync(string callId)
    {
        if (_hub?.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("Decline", callId); } catch { }
    }

    /// <summary>Zəng səhifəsinin ünvanı: token "#t=" hissəsində (serverə getmir).</summary>
    public async Task<string> BuildCallUrlAsync(string? conversation, string? callId, bool video, string? title)
    {
        var token = await _api.GetValidAccessTokenAsync() ?? "";
        var query = callId != null ? "id=" + Uri.EscapeDataString(callId) : "c=" + Uri.EscapeDataString(conversation ?? "");
        return $"{AppConfig.SiteUrl}/Call?{query}&video={(video ? 1 : 0)}&app=1&lang={Lang.Current.Code}" +
               $"#t={Uri.EscapeDataString(token)}&title={Uri.EscapeDataString(title ?? "")}";
    }

    /// <summary>Kamera / mikrofon icazəsi (WebView-dan əvvəl sistem icazəsi lazımdır).</summary>
    public static async Task<bool> EnsurePermissionsAsync(bool video)
    {
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted) return false;
        if (video && await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted) return false;
        return true;
    }
}
