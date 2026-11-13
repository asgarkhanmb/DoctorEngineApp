using System.Net.Http.Headers;
using DoctorEngine.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace DoctorEngine.App.Services;

/// <summary>
/// Canlı çat: saytla eyni API (api/chat) və SignalR hub (/chatHub).
/// Sayt və tətbiq istifadəçiləri eyni söhbətdədir — mesajlar hər iki tərəfdə dərhal görünür.
/// </summary>
public class ChatClient : IAsyncDisposable
{
    private readonly ApiClient _api;
    private HubConnection? _hub;

    public event Action<ChatMessageDto>? MessageReceived;
    public event Action<ChatMessageDto>? MessageEdited;
    public event Action<int>? MessageDeleted;
    /// <summary>"online" | "connecting" | "offline"</summary>
    public event Action<string>? StateChanged;

    public ChatClient(ApiClient api) => _api = api;

    public Task<ChatPageDto> GetPageAsync(int? beforeId = null, int take = 30) =>
        _api.GetAsync<ChatPageDto>($"api/chat/messages?take={take}" + (beforeId.HasValue ? $"&beforeId={beforeId}" : ""));

    public Task<UnreadDto> GetUnreadAsync() => _api.GetAsync<UnreadDto>("api/chat/unread");

    public Task MarkReadAsync(int lastId) =>
        _api.PostFormAsync("api/chat/read", () => new FormUrlEncodedContent(new Dictionary<string, string> { ["lastId"] = lastId.ToString() }));

    public Task<ChatMessageDto> SendAsync(string? text, FileResult? file = null, string? fileContentType = null) =>
        _api.PostFormAsync<ChatMessageDto>("api/chat/send", () => BuildForm(text, file, fileContentType));

    /// <summary>Səs mesajı (yazılmış fayl yolu ilə).</summary>
    public Task<ChatMessageDto> SendAudioAsync(string path, string contentType) =>
        _api.PostFormAsync<ChatMessageDto>("api/chat/send", () =>
        {
            var form = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(File.ReadAllBytes(path));
            bytes.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(bytes, "File", "ses" + Path.GetExtension(path));
            return form;
        });

    public Task<ChatMessageDto> EditAsync(int id, string? text, bool removeAttachment) =>
        _api.PostFormAsync<ChatMessageDto>("api/chat/edit", () =>
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(id.ToString()), "Id" },
                { new StringContent(removeAttachment ? "true" : "false"), "RemoveAttachment" }
            };
            if (!string.IsNullOrWhiteSpace(text)) form.Add(new StringContent(text), "Message");
            return form;
        });

    public Task DeleteAsync(int id) =>
        _api.PostFormAsync($"api/chat/delete/{id}", () => new StringContent(""));

    // ================= CANLI BAĞLANTI =================

    public async Task ConnectAsync()
    {
        if (_hub != null) return;
        _hub = new HubConnectionBuilder()
            .WithUrl(AppConfig.BaseUrl + "/chatHub", o =>
            {
                // WebSocket header göndərə bilmir — token ?access_token= ilə gedir (server bunu gözləyir)
                o.AccessTokenProvider = () => _api.GetValidAccessTokenAsync();
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        _hub.On<ChatMessageDto>("ReceiveMessage", m => MainThread.BeginInvokeOnMainThread(() => MessageReceived?.Invoke(m)));
        _hub.On<ChatMessageDto>("EditMessage", m => MainThread.BeginInvokeOnMainThread(() => MessageEdited?.Invoke(m)));
        _hub.On<int>("DeleteMessage", id => MainThread.BeginInvokeOnMainThread(() => MessageDeleted?.Invoke(id)));
        _hub.Reconnecting += _ => { Raise("connecting"); return Task.CompletedTask; };
        _hub.Reconnected += _ => { Raise("online"); return Task.CompletedTask; };
        _hub.Closed += async _ =>
        {
            Raise("offline");
            await Task.Delay(5000);
            await StartSafeAsync();
        };

        await StartSafeAsync();
    }

    private async Task StartSafeAsync()
    {
        if (_hub == null || _hub.State != HubConnectionState.Disconnected) return;
        Raise("connecting");
        try
        {
            await _hub.StartAsync();
            Raise("online");
        }
        catch
        {
            Raise("offline");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hub == null) return;
        var hub = _hub;
        _hub = null;
        try { await hub.DisposeAsync(); } catch { }
        Raise("offline");
    }

    private void Raise(string state) => MainThread.BeginInvokeOnMainThread(() => StateChanged?.Invoke(state));

    private static MultipartFormDataContent BuildForm(string? text, FileResult? file, string? contentType)
    {
        var form = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(text)) form.Add(new StringContent(text.Trim()), "Message");
        if (file != null)
        {
            var stream = new StreamContent(File.OpenRead(file.FullPath));
            stream.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? file.ContentType ?? "application/octet-stream");
            form.Add(stream, "File", file.FileName);
        }
        return form;
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());
}
