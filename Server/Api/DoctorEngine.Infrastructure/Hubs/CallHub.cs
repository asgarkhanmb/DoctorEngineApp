using System.Collections.Concurrent;
using System.Text.Json;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Localization;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace DoctorEngineApp.Hubs
{
    /// <summary>
    /// Video / səsli zənglər (WebRTC siqnalizasiyası) — /callHub. Yalnız saytda işləyir: brauzer də, mobil tətbiq də
    /// (WebView-dakı zəng səhifəsi və zəng bildirişi) bu prosesə qoşulur — siqnallar bir yerdə olur.
    ///  - 1:1: müştəri ↔ ustalar (ustalardan biri cavab verəndə digərlərində zəng susur)
    ///  - Qrup zəngi: hər iştirakçı hər biri ilə birbaşa (mesh, ~6 nəfərə qədər)
    /// Server → klient: IncomingCall, CallAnswered, CallCancelled, CallDeclined, ParticipantJoined, ParticipantLeft, Signal, CallEnded
    /// </summary>
    [Authorize]
    public class CallHub : Hub
    {
        public const int MaxParticipants = 8;

        private sealed class CallState
        {
            public required string Id { get; init; }
            public required string Conversation { get; init; }
            public required bool IsGroup { get; init; }
            public required bool Video { get; init; }
            public required string Caller { get; init; }
            public required bool CallerIsAdmin { get; init; }
            public required string GroupTitle { get; init; }
            public DateTime Started { get; } = DateTime.UtcNow;
            public DateTime? Answered { get; set; }
            public bool Ended { get; set; }
            /// <summary>connectionId → (userName, ad)</summary>
            public ConcurrentDictionary<string, (string User, string Name)> Participants { get; } = new();
            /// <summary>Zəng çalınan SignalR qrupları (cavab / ləğv bildirişi üçün).</summary>
            public List<string> RingGroups { get; init; } = new();
        }

        private static readonly ConcurrentDictionary<string, CallState> Calls = new();
        private static readonly ConcurrentDictionary<string, string> ConnectionCall = new();

        public const string AdminsGroup = "call-admins";
        public static string UserGroup(string user) => "call-u-" + user.ToLowerInvariant();

        private readonly IChatService _chat;
        private readonly IChatNotifier _notifier;
        private readonly UserManager<AppUser> _users;
        private readonly ISiteSettingService _site;
        private readonly ILogger<CallHub> _logger;

        public CallHub(IChatService chat, IChatNotifier notifier, UserManager<AppUser> users, ISiteSettingService site, ILogger<CallHub> logger)
        {
            _chat = chat;
            _notifier = notifier;
            _users = users;
            _site = site;
            _logger = logger;
        }

        private string Me => Context.User?.Identity?.Name ?? "";
        private bool IsAdmin => Context.User?.IsInRole(nameof(Roles.Admin)) == true;

        public override async Task OnConnectedAsync()
        {
            if (Me.Length > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(Me));
                if (IsAdmin) await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (ConnectionCall.TryGetValue(Context.ConnectionId, out var callId))
                await LeaveInternalAsync(callId, Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        private async Task<string> DisplayNameAsync(string user)
        {
            var u = await _users.FindByNameAsync(user);
            return string.IsNullOrWhiteSpace(u?.FullName) ? user : u!.FullName;
        }

        /// <summary>Zəng başlat. Qaytarır: { callId, title, isGroup }.</summary>
        public async Task<object> StartCall(string? conversation, bool video)
        {
            var me = Me;
            var key = await _chat.ResolveConversationAsync(me, IsAdmin, conversation)
                ?? throw new HubException(L.T("Yazışma tapılmadı."));
            var isDirect = ChatService.IsDirectKey(key);
            var isGroup = ChatService.IsGroupKey(key) && !isDirect;

            // Kimə çalınır
            var ring = new List<string>();
            string groupTitle = "";
            if (isDirect)
            {
                // Şəxsi yazışma: yalnız qarşı tərəfə
                var audience = await _chat.GetAudienceAsync(key);
                ring.AddRange(audience.UserNames.Where(u => !string.Equals(u, me, StringComparison.OrdinalIgnoreCase)).Select(UserGroup));
            }
            else if (isGroup)
            {
                var audience = await _chat.GetAudienceAsync(key);
                if (audience.Everyone) ring.Add("call-all");
                else ring.AddRange(audience.UserNames.Select(UserGroup));
                ring.Add(AdminsGroup);
                var g = await _chat.GetGroupAsync(ChatService.GroupIdOf(key)!.Value, me, IsAdmin);
                groupTitle = g?.Name ?? "";
            }
            else if (IsAdmin) ring.Add(UserGroup(key));     // usta → müştəri
            else ring.Add(AdminsGroup);                     // müştəri → ustalar

            var call = new CallState
            {
                Id = Guid.NewGuid().ToString("N"),
                Conversation = key,
                IsGroup = isGroup,
                Video = video,
                Caller = me,
                CallerIsAdmin = IsAdmin,
                GroupTitle = groupTitle,
                RingGroups = ring.Distinct().ToList()
            };
            var myName = await DisplayNameAsync(me);
            call.Participants[Context.ConnectionId] = (me, myName);
            Calls[call.Id] = call;
            ConnectionCall[Context.ConnectionId] = call.Id;

            var payload = new
            {
                callId = call.Id,
                conversation = key,
                isGroup,
                video,
                from = me,
                fromName = myName,
                fromIsAdmin = IsAdmin,
                title = isGroup ? groupTitle : myName
            };
            // Özümə (başqa cihazlarımda) çalınmasın
            var targets = call.RingGroups.Where(g => g != "call-all").ToList();
            if (call.RingGroups.Contains("call-all"))
                await Clients.AllExcept(Context.ConnectionId).SendAsync("IncomingCall", payload);
            else if (targets.Count > 0)
                await Clients.GroupExcept(targets[0], Context.ConnectionId).SendAsync("IncomingCall", payload);
            foreach (var g in targets.Skip(1))
                await Clients.GroupExcept(g, Context.ConnectionId).SendAsync("IncomingCall", payload);

            // Zəng edənin ekranında: qrupun adı, müştərinin adı (usta zəng edəndə) və ya saytın adı (müştəri ustalara zəng edəndə)
            var title = isGroup ? groupTitle
                : IsAdmin ? await DisplayNameAsync(key)
                : (await _site.GetAsync())?.SiteName ?? "DoctorEngine";
            return new { callId = call.Id, title, isGroup, video };
        }

        /// <summary>Zəngə qoşul (cavab ver). Qaytarır: mövcud iştirakçılar — yeni qoşulan onlara "offer" göndərir.</summary>
        public async Task<object> Join(string callId)
        {
            if (!Calls.TryGetValue(callId, out var call) || call.Ended)
                throw new HubException(L.T("Zəng artıq bitib."));
            // İcazə: həmin yazışmanın iştirakçısı
            if (await _chat.ResolveConversationAsync(Me, IsAdmin, call.Conversation) == null)
                throw new HubException(L.T("Yazışma tapılmadı."));
            if (call.Participants.Count >= MaxParticipants)
                throw new HubException(L.T("Zəngdə yer yoxdur (maksimum {0} nəfər).", MaxParticipants));

            var existing = call.Participants.Select(p => new { connectionId = p.Key, userName = p.Value.User, name = p.Value.Name }).ToList();
            var myName = await DisplayNameAsync(Me);
            call.Participants[Context.ConnectionId] = (Me, myName);
            ConnectionCall[Context.ConnectionId] = callId;

            var firstAnswer = call.Answered == null;
            call.Answered ??= DateTime.UtcNow;

            foreach (var p in existing)
                await Clients.Client(p.connectionId).SendAsync("ParticipantJoined", new { connectionId = Context.ConnectionId, userName = Me, name = myName });

            // 1:1 — başqa cihazlarda / ustalarda zəng susur
            if (!call.IsGroup && firstAnswer)
                foreach (var g in call.RingGroups)
                    await Clients.GroupExcept(g, Context.ConnectionId).SendAsync("CallAnswered", new { callId, by = Me });

            return new
            {
                callId,
                conversation = call.Conversation,
                isGroup = call.IsGroup,
                video = call.Video,
                title = call.IsGroup ? call.GroupTitle : existing.FirstOrDefault()?.name ?? "",
                participants = existing
            };
        }

        /// <summary>Gələn zəngi rədd et. 1:1-də zəng edən xəbər tutur və zəng bitir.</summary>
        public async Task Decline(string callId)
        {
            if (!Calls.TryGetValue(callId, out var call) || call.IsGroup || call.Answered != null) return;
            // Usta rədd edəndə — digər ustalarda hələ çalsın (yalnız müştəri rədd edəndə bitir)
            if (!call.CallerIsAdmin && IsAdmin) return;
            foreach (var p in call.Participants.Keys)
                await Clients.Client(p).SendAsync("CallDeclined", new { callId, by = Me });
            await EndAsync(call, missed: true);
        }

        /// <summary>WebRTC siqnalı (offer / answer / ICE) — eyni zəngdəki iştirakçıya.</summary>
        public async Task Signal(string toConnectionId, JsonElement payload)
        {
            if (!ConnectionCall.TryGetValue(Context.ConnectionId, out var callId) ||
                !Calls.TryGetValue(callId, out var call) ||
                !call.Participants.ContainsKey(toConnectionId))
                return;
            if (payload.ValueKind != JsonValueKind.Object || payload.GetRawText().Length > 64_000) return;
            await Clients.Client(toConnectionId).SendAsync("Signal", Context.ConnectionId, payload);
        }

        public Task Leave(string callId) => LeaveInternalAsync(callId, Context.ConnectionId);

        private async Task LeaveInternalAsync(string callId, string connectionId)
        {
            ConnectionCall.TryRemove(connectionId, out _);
            if (!Calls.TryGetValue(callId, out var call)) return;
            if (!call.Participants.TryRemove(connectionId, out _)) return;

            foreach (var p in call.Participants.Keys)
                await Clients.Client(p).SendAsync("ParticipantLeft", connectionId);

            // 1:1 — biri çıxanda bitir; qrupda — bir nəfər qalanda (və ya heç kim cavab vermədən zəng edən çıxanda)
            if (call.Participants.Count <= 1)
            {
                foreach (var p in call.Participants.Keys)
                    await Clients.Client(p).SendAsync("CallEnded", new { callId });
                await EndAsync(call, missed: call.Answered == null);
            }
        }

        private async Task EndAsync(CallState call, bool missed)
        {
            if (call.Ended) return;
            call.Ended = true;
            Calls.TryRemove(call.Id, out _);
            foreach (var p in call.Participants.Keys) ConnectionCall.TryRemove(p, out _);

            // Hələ çalan cihazlarda zəng dayansın
            foreach (var g in call.RingGroups)
            {
                if (g == "call-all") await Clients.All.SendAsync("CallCancelled", new { callId = call.Id });
                else await Clients.Group(g).SendAsync("CallCancelled", new { callId = call.Id });
            }

            // Çatda zəng qeydi (WhatsApp kimi): "📹 Video zəng · 2:31" və ya "Buraxılmış zəng"
            try
            {
                string text;
                var kind = call.Video ? "📹 " + L.T("Video zəng") : "📞 " + L.T("Səsli zəng");
                if (missed || call.Answered == null)
                    text = (call.Video ? "📹 " : "📞 ") + L.T("Buraxılmış zəng");
                else
                {
                    var d = DateTime.UtcNow - call.Answered.Value;
                    text = $"{kind} · {(int)d.TotalMinutes}:{d.Seconds:00}";
                }
                var result = await _chat.SendAsync(new ChatSendVM { Message = text }, call.Conversation, call.Caller, call.CallerIsAdmin);
                if (result.Status == ChatResultStatus.Ok)
                    await _notifier.MessageCreatedAsync(result.Message!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Zəng qeydi çata yazılmadı");
            }
        }
    }
}
