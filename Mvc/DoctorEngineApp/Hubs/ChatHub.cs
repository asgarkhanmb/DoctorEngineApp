using System.Collections.Concurrent;
using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DoctorEngineApp.Hubs
{
    /// <summary>
    /// Şəxsi çatın canlı kanalı (/chatHub). Yalnız daxil olmuş istifadəçilər.
    ///  - Müştəri yalnız öz qrupuna ("chat-u-{ad}") qoşulur — başqalarının mesajlarını almır
    ///  - Ustalar (admin) "chat-admins" qrupundadır — bütün yazışmaları alır
    /// Server → klient: ReceiveMessage, EditMessage, DeleteMessage, Receipt, Typing, Presence, ConversationUpdated
    /// Klient → server: Typing(conversation, isTyping), Heartbeat()
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        public const string AdminsGroup = "chat-admins";

        public static string UserGroup(string username) => "chat-u-" + username.ToLowerInvariant();

        /// <summary>İstifadəçinin öz cihazları (usta da daxil) — "yazışmanı sil" kimi şəxsi hadisələr üçün.</summary>
        public static string SelfGroup(string username) => "chat-self-" + username.ToLowerInvariant();

        // Bu prosesdəki açıq bağlantıların sayı (bir istifadəçi bir neçə tabda / cihazda ola bilər)
        private static readonly ConcurrentDictionary<string, int> Connections = new(StringComparer.OrdinalIgnoreCase);

        private readonly IChatService _chat;
        private readonly IChatNotifier _notifier;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IChatService chat, IChatNotifier notifier, ILogger<ChatHub> logger)
        {
            _chat = chat;
            _notifier = notifier;
            _logger = logger;
        }

        private string? Me => Context.User?.Identity?.Name;
        private bool IsAdmin => Context.User?.IsInRole(nameof(Roles.Admin)) == true;

        public override async Task OnConnectedAsync()
        {
            var me = Me;
            if (me != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, IsAdmin ? AdminsGroup : UserGroup(me));
                await Groups.AddToGroupAsync(Context.ConnectionId, SelfGroup(me));

                if (Connections.AddOrUpdate(me, 1, (_, n) => n + 1) == 1)
                {
                    await _chat.SetPresenceAsync(me, true);
                    await BroadcastPresenceAsync(me, IsAdmin);
                }

                // Cihaz qoşuldu — gözləyən mesajlar "çatdı" (✓✓), göndərənlər canlı görür
                try
                {
                    foreach (var receipt in await _chat.MarkAllDeliveredAsync(me, IsAdmin))
                        await _notifier.ReceiptAsync(receipt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Qoşulanda ✓✓ yenilənmədi: {User}", me);
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var me = Me;
            if (me != null)
            {
                var left = Connections.AddOrUpdate(me, 0, (_, n) => Math.Max(0, n - 1));
                if (left == 0)
                {
                    Connections.TryRemove(me, out _);
                    await _chat.SetPresenceAsync(me, false);
                    await BroadcastPresenceAsync(me, IsAdmin);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>Klient hər ~30 saniyədən bir çağırır: "onlayn" vəziyyəti köhnəlməsin.</summary>
        public Task Heartbeat() => Me is string me ? _chat.SetPresenceAsync(me, true) : Task.CompletedTask;

        /// <summary>"yazır…" göstəricisi (bazaya yazılmır).</summary>
        public async Task Typing(string? conversation, bool isTyping)
        {
            var me = Me;
            if (me == null) return;

            // Qrup: üzvlərə (+ ustalara), göndərənin adı ilə ("Ali yazır…")
            if (DoctorEngineApp.Services.ChatService.IsGroupKey(conversation))
            {
                var key = await _chat.ResolveConversationAsync(me, IsAdmin, conversation);
                if (key == null) return;
                var target = DoctorEngineApp.Services.ChatNotifier.Target(Clients, await _chat.GetAudienceAsync(key));
                await target.SendAsync("Typing", new { conversation = key, fromAdmin = IsAdmin, userName = me, isTyping, isGroup = true });
                return;
            }

            if (IsAdmin)
            {
                if (string.IsNullOrWhiteSpace(conversation) || conversation.Length > 256) return;
                var payload = new { conversation, fromAdmin = true, userName = me, isTyping };
                await Clients.Group(UserGroup(conversation)).SendAsync("Typing", payload);
                await Clients.OthersInGroup(AdminsGroup).SendAsync("Typing", payload);
            }
            else
            {
                await Clients.Group(AdminsGroup).SendAsync("Typing",
                    new { conversation = me, fromAdmin = false, userName = me, isTyping });
            }
        }

        private async Task BroadcastPresenceAsync(string username, bool isAdmin)
        {
            if (isAdmin)
            {
                // Müştərilər üçün "ustalar" bir tərəfdir: hər hansı usta onlayndırsa — onlayn
                var (online, lastSeen) = await _chat.GetSupportPresenceAsync();
                await Clients.All.SendAsync("Presence", new { conversation = (string?)null, isSupport = true, online, lastSeen });
            }
            else
            {
                var online = Connections.ContainsKey(username);
                await Clients.Group(AdminsGroup).SendAsync("Presence", new
                {
                    conversation = username,
                    isSupport = false,
                    online,
                    lastSeen = online ? null : DoctorEngineApp.Helpers.AzTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
        }
    }
}
