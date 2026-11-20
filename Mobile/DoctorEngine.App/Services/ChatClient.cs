using System.Net.Http.Headers;
using DoctorEngine.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace DoctorEngine.App.Services;

/// <summary>
/// Şəxsi çat (WhatsApp kimi): API (api/chat) və SignalR hub (/chatHub).
/// Müştəri yalnız öz yazışmasını görür; usta (admin) "conversation" ilə müştərini seçir.
/// </summary>
public class ChatClient : IAsyncDisposable
{
    private readonly ApiClient _api;
    private HubConnection? _hub;
    private IDispatcherTimer? _heartbeat;

    public event Action<ChatMessageDto>? MessageReceived;
    public event Action<ChatMessageDto>? MessageEdited;
    public event Action<int>? MessageDeleted;
    public event Action<ChatReceiptDto>? ReceiptReceived;
    public event Action<ChatTypingDto>? TypingReceived;
    public event Action<ChatPresenceDto>? PresenceReceived;
    public event Action<ChatConversationDto>? ConversationUpdated;
    public event Action<string>? ConversationRemoved;
    /// <summary>Yazışma silindi (mənim digər cihazımda və ya usta hamı üçün sildi).</summary>
    public event Action<string>? ConversationCleared;
    /// <summary>Yazışma / qrup dəyişdi — siyahını yenilə.</summary>
    public event Action<string>? ConversationChanged;
    /// <summary>"online" | "connecting" | "offline"</summary>
    public event Action<string>? StateChanged;

    public ChatClient(ApiClient api) => _api = api;

    private static string Conv(string? conversation, bool first = false) =>
        string.IsNullOrEmpty(conversation) ? "" : (first ? "?" : "&") + "conversation=" + Uri.EscapeDataString(conversation);

    // ================= OXU =================

    public Task<ChatPageDto> GetPageAsync(string? conversation, int? beforeId = null, int take = 30) =>
        _api.GetAsync<ChatPageDto>($"api/chat/messages?take={take}" + (beforeId.HasValue ? $"&beforeId={beforeId}" : "") + Conv(conversation));

    /// <summary>"Çatlar": usta — bütün yazışmalar və qruplar; müştəri — ustalarla yazışma və qrupları.</summary>
    public Task<List<ChatConversationDto>> GetConversationsAsync() =>
        _api.GetAsync<List<ChatConversationDto>>("api/chat/conversations");

    public Task<List<ChatMessageDto>> SearchAsync(string? conversation, string query) =>
        _api.GetAsync<List<ChatMessageDto>>("api/chat/search?q=" + Uri.EscapeDataString(query) + Conv(conversation));

    public Task<UnreadDto> GetUnreadAsync() => _api.GetAsync<UnreadDto>("api/chat/unread");

    // ================= ✓✓ =================

    public Task MarkReadAsync(string? conversation, int lastId) => ReceiptAsync("api/chat/read", conversation, lastId);

    public Task MarkDeliveredAsync(string? conversation, int lastId) => ReceiptAsync("api/chat/delivered", conversation, lastId);

    private Task ReceiptAsync(string url, string? conversation, int lastId) =>
        _api.PostFormAsync(url, () =>
        {
            var fields = new Dictionary<string, string> { ["lastId"] = lastId.ToString() };
            if (!string.IsNullOrEmpty(conversation)) fields["conversation"] = conversation;
            return new FormUrlEncodedContent(fields);
        });

    // ================= YAZ =================

    public Task<ChatMessageDto> SendAsync(string? conversation, string? text, int? replyToId, FileResult? file = null, string? fileContentType = null) =>
        _api.PostFormAsync<ChatMessageDto>("api/chat/send", () =>
        {
            var form = BaseForm(conversation, replyToId);
            if (!string.IsNullOrWhiteSpace(text)) form.Add(new StringContent(text.Trim()), "Message");
            if (file != null)
            {
                var stream = new StreamContent(File.OpenRead(file.FullPath));
                stream.Headers.ContentType = new MediaTypeHeaderValue(fileContentType ?? file.ContentType ?? "application/octet-stream");
                form.Add(stream, "File", file.FileName);
            }
            return form;
        });

    /// <summary>Səs mesajı (yazılmış fayl yolu ilə).</summary>
    public Task<ChatMessageDto> SendAudioAsync(string? conversation, string path, string contentType, int? replyToId) =>
        _api.PostFormAsync<ChatMessageDto>("api/chat/send", () =>
        {
            var form = BaseForm(conversation, replyToId);
            var bytes = new ByteArrayContent(File.ReadAllBytes(path));
            bytes.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(bytes, "File", "ses" + Path.GetExtension(path));
            return form;
        });

    private static MultipartFormDataContent BaseForm(string? conversation, int? replyToId)
    {
        var form = new MultipartFormDataContent();
        if (!string.IsNullOrEmpty(conversation)) form.Add(new StringContent(conversation), "Conversation");
        if (replyToId is int r) form.Add(new StringContent(r.ToString()), "ReplyToId");
        return form;
    }

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

    /// <summary>Reaksiya: eyni emoji təkrar → götürülür.</summary>
    public Task<ChatMessageDto> ReactAsync(int id, string emoji) =>
        _api.PostFormAsync<ChatMessageDto>("api/chat/react", () => new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = id.ToString(),
            ["emoji"] = emoji
        }));

    // ================= QRUPLAR / YAYIM =================

    public Task<List<ChatUserDto>> GetUsersAsync() => _api.GetAsync<List<ChatUserDto>>("api/chat/users");

    public Task<ChatGroupInfoDto> GetGroupAsync(int id) => _api.GetAsync<ChatGroupInfoDto>($"api/chat/groups/{id}");

    /// <summary>Qrup yarat (id = null) və ya dəyiş. members — əlavə olunacaq üzvlər.</summary>
    public Task<ChatGroupInfoDto> SaveGroupAsync(int? id, string name, string? description, bool allUsers, bool adminsOnly, IEnumerable<string> members) =>
        _api.PostFormAsync<ChatGroupInfoDto>(id is int i ? $"api/chat/groups/{i}" : "api/chat/groups", () =>
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(name), "Name" },
                { new StringContent(allUsers ? "true" : "false"), "AllUsers" },
                { new StringContent(adminsOnly ? "true" : "false"), "AdminsOnly" }
            };
            if (!string.IsNullOrWhiteSpace(description)) form.Add(new StringContent(description), "Description");
            foreach (var m in members) form.Add(new StringContent(m), "Members");
            return form;
        });

    public Task<ChatGroupInfoDto> RemoveMemberAsync(int id, string userName) =>
        _api.PostFormAsync<ChatGroupInfoDto>($"api/chat/groups/{id}/remove", () =>
            new FormUrlEncodedContent(new Dictionary<string, string> { ["userName"] = userName }));

    /// <summary>Yeni çat ("+"): yaza biləcəyim istifadəçilər.</summary>
    public Task<List<ChatUserDto>> GetPeopleAsync(string? query) =>
        _api.GetAsync<List<ChatUserDto>>("api/chat/people" + (string.IsNullOrWhiteSpace(query) ? "" : "?q=" + Uri.EscapeDataString(query.Trim())));

    /// <summary>İstifadəçiyə yaz: yazışmanın açarı (şəxsi yazışma "d:{id}" və ya ustalarla yazışma).</summary>
    public async Task<string> StartDirectAsync(string userName) =>
        (await _api.PostFormAsync<DirectResultDto>("api/chat/direct", () =>
            new FormUrlEncodedContent(new Dictionary<string, string> { ["userName"] = userName }))).Conversation;

    /// <summary>Yazışmanı sil: forEveryone = false — yalnız mənim üçün; true — usta, hamı üçün.</summary>
    public Task ClearConversationAsync(string? conversation, bool forEveryone) =>
        _api.PostFormAsync("api/chat/clear", () =>
        {
            var fields = new Dictionary<string, string> { ["forEveryone"] = forEveryone ? "true" : "false" };
            if (!string.IsNullOrEmpty(conversation)) fields["conversation"] = conversation;
            return new FormUrlEncodedContent(fields);
        });

    public Task DeleteGroupAsync(int id) => _api.PostFormAsync($"api/chat/groups/{id}/delete", () => new StringContent(""));

    /// <summary>Yayım: mesaj hər müştərinin şəxsi yazışmasına ayrıca.</summary>
    public Task<BroadcastResultDto> BroadcastAsync(string text) =>
        _api.PostFormAsync<BroadcastResultDto>("api/chat/broadcast", () =>
            new MultipartFormDataContent { { new StringContent(text), "Message" } });

    /// <summary>"yazır…" (usta və qrup üçün conversation lazımdır).</summary>
    public async Task TypingAsync(string? conversation, bool isTyping)
    {
        if (_hub?.State != HubConnectionState.Connected) return;
        try { await _hub.InvokeAsync("Typing", conversation, isTyping); } catch { }
    }

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

        _hub.On<ChatMessageDto>("ReceiveMessage", m => Ui(() => MessageReceived?.Invoke(m)));
        _hub.On<ChatMessageDto>("EditMessage", m => Ui(() => MessageEdited?.Invoke(m)));
        _hub.On<int>("DeleteMessage", id => Ui(() => MessageDeleted?.Invoke(id)));
        _hub.On<ChatReceiptDto>("Receipt", r => Ui(() => ReceiptReceived?.Invoke(r)));
        _hub.On<ChatTypingDto>("Typing", t => Ui(() => TypingReceived?.Invoke(t)));
        _hub.On<ChatPresenceDto>("Presence", p => Ui(() => PresenceReceived?.Invoke(p)));
        _hub.On<ChatConversationDto>("ConversationUpdated", c => Ui(() => ConversationUpdated?.Invoke(c)));
        _hub.On<string>("ConversationRemoved", c => Ui(() => ConversationRemoved?.Invoke(c)));
        _hub.On<ConversationClearedDto>("ConversationCleared", e => Ui(() => ConversationCleared?.Invoke(e.Conversation)));
        _hub.On<string>("ConversationChanged", c => Ui(() => ConversationChanged?.Invoke(c)));
        _hub.Reconnecting += _ => { Raise("connecting"); return Task.CompletedTask; };
        _hub.Reconnected += _ => { Raise("online"); return Task.CompletedTask; };
        _hub.Closed += async _ =>
        {
            Raise("offline");
            await Task.Delay(5000);
            await StartSafeAsync();
        };

        // Onlayn vəziyyəti köhnəlməsin (server 90 saniyə siqnal almasa "son görülmə" göstərir)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _heartbeat = Application.Current?.Dispatcher.CreateTimer();
            if (_heartbeat == null) return;
            _heartbeat.Interval = TimeSpan.FromSeconds(30);
            _heartbeat.Tick += async (_, _) =>
            {
                if (_hub?.State == HubConnectionState.Connected)
                    try { await _hub.InvokeAsync("Heartbeat"); } catch { }
            };
            _heartbeat.Start();
        });

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
        _heartbeat?.Stop();
        _heartbeat = null;
        if (_hub == null) return;
        var hub = _hub;
        _hub = null;
        try { await hub.DisposeAsync(); } catch { }
        Raise("offline");
    }

    private static void Ui(Action a) => MainThread.BeginInvokeOnMainThread(a);

    private void Raise(string state) => Ui(() => StateChanged?.Invoke(state));

    public ValueTask DisposeAsync() => new(DisconnectAsync());
}
