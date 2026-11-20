using DoctorEngineApp.Localization;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    /// <summary>
    /// Çat (WhatsApp kimi):
    ///  - Şəxsi yazışma müştəri ↔ ustalar: ChatMessage.ConversationUserName = müştərinin adı, ✓✓ — ChatConversation
    ///  - Qrup: ConversationUserName = "g:{id}", üzvlər və ✓✓ — ChatGroupMember
    ///  - Yayım: eyni mesaj hər müştərinin şəxsi yazışmasına
    /// Cavab (ReplyToId), reaksiyalar (ChatReaction), onlayn / son görülmə (AppUser).
    /// </summary>
    public class ChatService : IChatService
    {
        public const int MaxMessageLength = 1000;
        public const long MaxImageBytes = 10L * 1024 * 1024;   // 10 MB
        public const long MaxVideoBytes = 100L * 1024 * 1024;  // 100 MB
        public const long MaxAudioBytes = 16L * 1024 * 1024;   // 16 MB (səs mesajı)

        /// <summary>Reaksiya üçün icazəli emojilər (WhatsApp-ın tez reaksiyaları + bir neçə əlavə).</summary>
        public static readonly string[] AllowedReactions =
            { "👍", "❤️", "😂", "😮", "😢", "🙏", "👏", "🔥", "🎉", "😍", "🤔", "👌", "✅", "🔧", "🚗" };

        /// <summary>Tətbiq / brauzer bu qədər müddətdə bir "ürək döyüntüsü" göndərməsə — onlayn sayılmır.</summary>
        private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(90);

        private const string UploadFolder = "uploads/chat";
        private const int ReplySnippetLength = 160;

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChatService> _logger;

        public ChatService(AppDbContext context, IWebHostEnvironment env, ILogger<ChatService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        /// <summary>Üzvlərə əsaslanan yazışma: qrup ("g:{id}") və ya iki nəfərin şəxsi yazışması ("d:{id}").</summary>
        public static bool IsGroupKey(string? conversation) =>
            conversation != null && (conversation.StartsWith(ChatGroup.KeyPrefix, StringComparison.Ordinal) ||
                                     conversation.StartsWith(ChatGroup.DirectPrefix, StringComparison.Ordinal));

        /// <summary>İki istifadəçinin şəxsi yazışması ("d:{id}").</summary>
        public static bool IsDirectKey(string? conversation) =>
            conversation != null && conversation.StartsWith(ChatGroup.DirectPrefix, StringComparison.Ordinal);

        public static int? GroupIdOf(string? conversation) =>
            IsGroupKey(conversation) && int.TryParse(conversation![2..], out var id) ? id : null;

        /// <summary>Açar qrupun növünə uyğun olmalıdır ("d:5" qrup 5-i açmasın və əksinə).</summary>
        private static bool KeyMatches(string? conversation, bool isDirect) => IsDirectKey(conversation) == isDirect;

        // ================= YAZIŞMA / İCAZƏ =================

        public async Task<string?> ResolveConversationAsync(string username, bool isAdmin, string? conversation)
        {
            if (GroupIdOf(conversation) is int gid)
            {
                var g = await _context.ChatGroups.AsNoTracking().Where(x => x.Id == gid)
                    .Select(x => new { x.IsDirect }).FirstOrDefaultAsync();
                if (g == null || !KeyMatches(conversation, g.IsDirect)) return null;
                return await CanAccessGroupAsync(gid, username, isAdmin)
                    ? (g.IsDirect ? ChatGroup.DirectPrefix : ChatGroup.KeyPrefix) + gid : null;
            }

            if (!isAdmin)
            {
                // Müştəri yalnız öz şəxsi yazışmasını görür (başqa ad verilərsə — rədd)
                return string.IsNullOrWhiteSpace(conversation) || string.Equals(conversation, username, StringComparison.OrdinalIgnoreCase)
                    ? username : null;
            }
            if (string.IsNullOrWhiteSpace(conversation)) return null;

            // Ad dəqiq bazadakı kimi qaytarılır (böyük/kiçik hərf fərqi olmasın)
            var normalized = conversation.Trim().ToUpperInvariant();
            return await _context.Users.AsNoTracking()
                .Where(u => u.NormalizedUserName == normalized)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> CanAccessGroupAsync(int groupId, string username, bool isAdmin)
        {
            var group = await _context.ChatGroups.AsNoTracking().Where(g => g.Id == groupId)
                .Select(g => new { g.AllUsers, g.IsDirect }).FirstOrDefaultAsync();
            if (group == null) return false;
            // Şəxsi yazışma — yalnız iki üzv (ustalar da görmür)
            if (group.IsDirect)
                return await _context.ChatGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserName == username && m.IsExplicit);
            if (isAdmin || group.AllUsers) return true;
            return await _context.ChatGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserName == username && m.IsExplicit);
        }

        public async Task<ChatAudience> GetAudienceAsync(string conversation)
        {
            if (GroupIdOf(conversation) is not int gid)
                return new ChatAudience(false, false, new[] { conversation });

            var group = await _context.ChatGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gid);
            if (group == null) return new ChatAudience(true, false, Array.Empty<string>());
            if (group.AllUsers) return new ChatAudience(true, true, Array.Empty<string>());

            var members = await _context.ChatGroupMembers.AsNoTracking()
                .Where(m => m.GroupId == gid && m.IsExplicit)
                .Select(m => m.UserName).ToListAsync();
            return new ChatAudience(true, false, members, Private: group.IsDirect);
        }

        // ================= OXU =================

        public async Task<ChatPageDto> GetPageAsync(string conversation, string viewer, bool viewerIsAdmin, int? beforeId, int take)
        {
            take = Math.Clamp(take, 1, 100);

            // "Yazışmanı sil" (mənim üçün) — o vaxta qədərki mesajlar görünmür
            var cleared = await ClearedIdAsync(viewer, conversation);
            var query = _context.ChatMessages.AsNoTracking()
                .Where(m => !m.SoftDeleted && m.ConversationUserName == conversation && m.Id > cleared);
            if (beforeId.HasValue)
                query = query.Where(m => m.Id < beforeId.Value);

            // Ən yenilərdən take+1 götür (daha köhnə mesaj olub-olmadığını bilmək üçün)
            var items = await query
                .OrderByDescending(m => m.Id)
                .Take(take + 1)
                .ToListAsync();

            var hasMore = items.Count > take;
            var page = items.Take(take).Reverse().ToList();

            return new ChatPageDto
            {
                Messages = await MapAsync(page),
                HasMore = hasMore,
                Peer = GroupIdOf(conversation) is int gid
                    ? await GetGroupPeerAsync(gid, viewer, viewerIsAdmin)
                    : await GetPeerAsync(conversation, viewerIsAdmin)
            };
        }

        public async Task<List<ChatMessageDto>> SearchAsync(string conversation, string viewer, string query)
        {
            var cleared = await ClearedIdAsync(viewer, conversation);
            query = (query ?? "").Trim();
            if (query.Length < 2) return new();
            if (query.Length > 100) query = query[..100];

            var items = await _context.ChatMessages.AsNoTracking()
                .Where(m => !m.SoftDeleted && m.ConversationUserName == conversation && m.Id > cleared &&
                            m.Message != null && m.Message.Contains(query))
                .OrderByDescending(m => m.Id)
                .Take(50)
                .ToListAsync();

            return await MapAsync(items);
        }

        // ================= "ÇATLAR" SİYAHISI =================

        public async Task<List<ChatConversationDto>> GetConversationsAsync(string viewer, bool isAdmin)
        {
            var rows = new List<ChatConversationDto>();
            var clears = await _context.ChatClears.AsNoTracking().Where(c => c.UserName == viewer)
                .ToDictionaryAsync(c => c.Conversation, c => c.ClearedId);

            if (isAdmin)
            {
                var convs = await _context.ChatConversations.AsNoTracking()
                    .OrderByDescending(c => c.LastMessageId)
                    .Take(300)
                    .ToListAsync();
                rows.AddRange(await BuildConversationRowsAsync(convs));
            }
            else
            {
                rows.Add(await BuildSupportRowAsync(viewer));
            }

            rows.AddRange(await BuildGroupRowsAsync(viewer, isAdmin));

            // "Mənim üçün silinmiş" yazışmalar: yeni mesaj yoxdursa siyahıda görünmür (müştərinin ustalar sətri həmişə qalır)
            var result = new List<ChatConversationDto>();
            foreach (var r in rows)
            {
                if (!clears.TryGetValue(r.Conversation, out var cl) || cl <= 0) { result.Add(r); continue; }
                if (r.IsSupport)
                {
                    if (r.LastMessageId <= cl || r.LastMessageId == int.MaxValue)
                    {
                        r.LastMessage = L.T("Ustalarla yazışın");
                        r.LastDate = "";
                        r.LastDelivered = r.LastRead = false;
                        r.LastFromAdmin = true;
                        r.Unread = 0;
                    }
                    result.Add(r);
                    continue;
                }
                if (r.LastMessageId <= cl) continue;
                if (r.Unread > 0) r.Unread = await CountUnreadAfterAsync(r.Conversation, viewer, isAdmin, cl);
                result.Add(r);
            }
            return result.OrderByDescending(r => r.LastMessageId).ToList();
        }

        public async Task<ChatConversationDto?> GetConversationAsync(string conversation)
        {
            if (IsGroupKey(conversation)) return null;
            var conv = await _context.ChatConversations.AsNoTracking().FirstOrDefaultAsync(c => c.UserName == conversation);
            if (conv == null) return null;
            return (await BuildConversationRowsAsync(new List<ChatConversation> { conv })).FirstOrDefault();
        }

        /// <summary>Müştəri üçün: ustalarla yazışma sətri (hələ mesaj olmasa da göstərilir).</summary>
        private async Task<ChatConversationDto> BuildSupportRowAsync(string viewer)
        {
            var conv = await _context.ChatConversations.AsNoTracking().FirstOrDefaultAsync(c => c.UserName == viewer);
            var site = await _context.SiteSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
            var (online, lastSeen) = await GetSupportPresenceAsync();
            var last = conv == null ? null : await _context.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == conv.LastMessageId);
            var unread = conv == null ? 0 : await _context.ChatMessages
                .CountAsync(m => !m.SoftDeleted && m.ConversationUserName == viewer && m.SenderIsAdmin && m.Id > conv.UserReadId);

            return new ChatConversationDto
            {
                Conversation = viewer,
                IsSupport = true,
                Title = string.IsNullOrWhiteSpace(site?.SiteName) ? SiteDefaults.Name : site!.SiteName,
                AvatarUrl = string.IsNullOrEmpty(site?.LogoImage) ? null : "/uploads/site/" + site!.LogoImage,
                Online = online,
                LastSeen = lastSeen,
                LastMessageId = conv?.LastMessageId ?? int.MaxValue,   // mesaj yoxdursa da yuxarıda dursun
                LastMessage = last == null ? L.T("Ustalarla yazışın") : Preview(last),
                LastFromAdmin = last?.SenderIsAdmin ?? false,
                LastDate = Iso(last?.CreatedDate) ?? "",
                Unread = unread,
                // Müştərinin öz mesajı üçün ✓✓ — ustaların göstəriciləri
                LastDelivered = last != null && !last.SenderIsAdmin && conv!.AdminDeliveredId >= last.Id,
                LastRead = last != null && !last.SenderIsAdmin && conv!.AdminReadId >= last.Id
            };
        }

        private async Task<List<ChatConversationDto>> BuildConversationRowsAsync(List<ChatConversation> convs)
        {
            if (convs.Count == 0) return new();

            var names = convs.Select(c => c.UserName).ToList();
            var lastIds = convs.Select(c => c.LastMessageId).ToList();

            var users = await _context.Users.AsNoTracking()
                .Where(u => names.Contains(u.UserName!))
                .Select(u => new { u.UserName, u.FullName, u.ProfileImage, u.ChatOnline, u.ChatLastSeen })
                .ToListAsync();
            var userMap = users.ToDictionary(u => u.UserName!, StringComparer.OrdinalIgnoreCase);

            var lastMessages = await _context.ChatMessages.AsNoTracking()
                .Where(m => lastIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            // Oxunmamış: müştərinin ustaların oxuduğundan sonrakı mesajları
            var unread = await (
                from m in _context.ChatMessages
                join c in _context.ChatConversations on m.ConversationUserName equals c.UserName
                where !m.SoftDeleted && !m.SenderIsAdmin && m.Id > c.AdminReadId && names.Contains(c.UserName)
                group m by c.UserName into g
                select new { UserName = g.Key, Count = g.Count() }
            ).ToDictionaryAsync(x => x.UserName, x => x.Count);

            var now = AzTime.Now;
            var rows = new List<ChatConversationDto>();
            foreach (var c in convs)
            {
                userMap.TryGetValue(c.UserName, out var u);
                lastMessages.TryGetValue(c.LastMessageId, out var last);
                var online = u != null && IsOnline(u.ChatOnline, u.ChatLastSeen, now);

                rows.Add(new ChatConversationDto
                {
                    Conversation = c.UserName,
                    Title = string.IsNullOrWhiteSpace(u?.FullName) ? c.UserName : u!.FullName,
                    AvatarUrl = AvatarUrl(u?.ProfileImage),
                    Online = online,
                    LastSeen = online ? null : Iso(u?.ChatLastSeen),
                    LastMessageId = c.LastMessageId,
                    LastMessage = last == null ? null : Preview(last),
                    LastFromAdmin = last?.SenderIsAdmin ?? false,
                    LastDate = Iso(last?.CreatedDate ?? c.LastMessageDate) ?? "",
                    Unread = unread.GetValueOrDefault(c.UserName),
                    LastDelivered = last != null && last.SenderIsAdmin && c.UserDeliveredId >= last.Id,
                    LastRead = last != null && last.SenderIsAdmin && c.UserReadId >= last.Id
                });
            }
            return rows;
        }

        private async Task<List<ChatConversationDto>> BuildGroupRowsAsync(string viewer, bool isAdmin)
        {
            // Şəxsi yazışma ilk mesajdan sonra siyahıda görünür (WhatsApp kimi)
            var groups = await AccessibleGroups(viewer, isAdmin).AsNoTracking()
                .Where(g => !g.IsDirect || g.LastMessageId > 0).ToListAsync();
            if (groups.Count == 0) return new();
            var peers = await DirectPeersAsync(groups.Where(g => g.IsDirect).Select(g => g.Id).ToList(), viewer);

            var ids = groups.Select(g => g.Id).ToList();
            var keys = groups.Select(g => g.Key).ToList();
            var lastIds = groups.Select(g => g.LastMessageId).ToList();

            var lastMessages = await _context.ChatMessages.AsNoTracking()
                .Where(m => lastIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
            var myRows = await _context.ChatGroupMembers.AsNoTracking()
                .Where(m => ids.Contains(m.GroupId) && m.UserName == viewer)
                .ToDictionaryAsync(m => m.GroupId);
            var minRead = await MinPointersAsync(ids);

            var rows = new List<ChatConversationDto>();
            foreach (var g in groups)
            {
                lastMessages.TryGetValue(g.LastMessageId, out var last);
                var readId = myRows.TryGetValue(g.Id, out var me) ? me.ReadId : 0;
                var unread = await _context.ChatMessages.CountAsync(m =>
                    !m.SoftDeleted && m.ConversationUserName == g.Key && m.Id > readId && m.UserName != viewer);
                var mineLast = last != null && last.UserName == viewer;
                minRead.TryGetValue(g.Id, out var ptr);

                if (g.IsDirect)
                {
                    var other = peers.GetValueOrDefault(g.Id);
                    rows.Add(new ChatConversationDto
                    {
                        Conversation = g.Key,
                        IsDirect = true,
                        PeerUserName = other?.UserName,
                        Title = other?.Title ?? L.T("İstifadəçi"),
                        AvatarUrl = other?.AvatarUrl,
                        Online = other?.Online ?? false,
                        LastSeen = other?.LastSeen,
                        LastMessageId = g.LastMessageId,
                        LastMessage = last == null ? "" : Preview(last),
                        LastFromAdmin = mineLast,   // ✓✓ yalnız mənim son mesajımda
                        LastDate = Iso(last?.CreatedDate ?? g.LastMessageDate) ?? "",
                        Unread = unread,
                        LastDelivered = mineLast && ptr.Delivered >= last!.Id,
                        LastRead = mineLast && ptr.Read >= last!.Id
                    });
                    continue;
                }

                rows.Add(new ChatConversationDto
                {
                    Conversation = g.Key,
                    IsGroup = true,
                    Title = g.Name,
                    LastMessageId = Math.Max(g.LastMessageId, 0),
                    LastMessage = last == null ? (g.Description ?? L.T("Qrup yaradıldı")) : Preview(last),
                    LastSender = last == null ? null : mineLast ? L.T("Siz") : SenderDisplay(last, isAdmin),
                    LastFromAdmin = mineLast,   // ✓✓ yalnız mənim son mesajımda
                    LastDate = Iso(last?.CreatedDate ?? g.CreatedDate) ?? "",
                    Unread = unread,
                    LastDelivered = mineLast && ptr.Delivered >= last!.Id,
                    LastRead = mineLast && ptr.Read >= last!.Id
                });
            }
            return rows;
        }

        private IQueryable<ChatGroup> AccessibleGroups(string viewer, bool isAdmin) =>
            isAdmin
                // Usta bütün qrupları görür, amma başqalarının şəxsi yazışmalarını yox
                ? _context.ChatGroups.Where(g => !g.IsDirect ||
                    _context.ChatGroupMembers.Any(m => m.GroupId == g.Id && m.UserName == viewer && m.IsExplicit))
                : _context.ChatGroups.Where(g => (g.AllUsers && !g.IsDirect) ||
                    _context.ChatGroupMembers.Any(m => m.GroupId == g.Id && m.UserName == viewer && m.IsExplicit));

        private sealed record DirectPeer(string UserName, string Title, string? AvatarUrl, bool Online, string? LastSeen);

        /// <summary>Şəxsi yazışmalarda qarşı tərəf (groupId → istifadəçi).</summary>
        private async Task<Dictionary<int, DirectPeer>> DirectPeersAsync(List<int> groupIds, string viewer)
        {
            if (groupIds.Count == 0) return new();
            var others = await _context.ChatGroupMembers.AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && m.UserName != viewer && m.IsExplicit)
                .Select(m => new { m.GroupId, m.UserName }).ToListAsync();
            var names = others.Select(o => o.UserName).Distinct().ToList();
            var users = (await _context.Users.AsNoTracking().Where(u => names.Contains(u.UserName!))
                    .Select(u => new { u.UserName, u.FullName, u.ProfileImage, u.ChatOnline, u.ChatLastSeen }).ToListAsync())
                .ToDictionary(u => u.UserName!, StringComparer.OrdinalIgnoreCase);
            var now = AzTime.Now;
            var result = new Dictionary<int, DirectPeer>();
            foreach (var o in others)
            {
                if (result.ContainsKey(o.GroupId)) continue;
                users.TryGetValue(o.UserName, out var u);
                var online = u != null && IsOnline(u.ChatOnline, u.ChatLastSeen, now);
                result[o.GroupId] = new DirectPeer(o.UserName,
                    string.IsNullOrWhiteSpace(u?.FullName) ? o.UserName : u!.FullName,
                    AvatarUrl(u?.ProfileImage), online, online ? null : Iso(u?.ChatLastSeen));
            }
            return result;
        }

        /// <summary>Qrupda hamının aldığı / oxuduğu son mesaj (qrupda ✓✓ — "hamı oxuyub").</summary>
        private async Task<Dictionary<int, (int Read, int Delivered)>> MinPointersAsync(List<int> groupIds)
        {
            var list = await _context.ChatGroupMembers.AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Read = g.Min(x => x.ReadId), Delivered = g.Min(x => x.DeliveredId), Count = g.Count() })
                .ToListAsync();
            // Tək üzv (yalnız göndərən) — heç kim oxumayıb
            return list.ToDictionary(x => x.GroupId, x => x.Count < 2 ? (0, 0) : (x.Read, x.Delivered));
        }

        private static string SenderDisplay(ChatMessage m, bool viewerIsAdmin) =>
            m.SenderIsAdmin && !viewerIsAdmin ? L.T("Usta") + " " + m.UserName : m.UserName;

        /// <summary>Qarşı tərəf: müştəri üçün ustalar, usta üçün müştəri.</summary>
        private async Task<ChatPeerDto> GetPeerAsync(string conversation, bool viewerIsAdmin)
        {
            var conv = await _context.ChatConversations.AsNoTracking().FirstOrDefaultAsync(c => c.UserName == conversation);
            var now = AzTime.Now;

            if (viewerIsAdmin)
            {
                var u = await _context.Users.AsNoTracking()
                    .Where(x => x.UserName == conversation)
                    .Select(x => new { x.FullName, x.ProfileImage, x.ChatOnline, x.ChatLastSeen })
                    .FirstOrDefaultAsync();
                var online = u != null && IsOnline(u.ChatOnline, u.ChatLastSeen, now);
                return new ChatPeerDto
                {
                    Conversation = conversation,
                    Title = string.IsNullOrWhiteSpace(u?.FullName) ? conversation : u!.FullName,
                    AvatarUrl = AvatarUrl(u?.ProfileImage),
                    Online = online,
                    LastSeen = online ? null : Iso(u?.ChatLastSeen),
                    ReadUpToId = conv?.UserReadId ?? 0,
                    DeliveredUpToId = conv?.UserDeliveredId ?? 0
                };
            }

            // Müştəri üçün: ustalar — hər hansı biri onlayndırsa "onlayn", yoxsa ən son görüləni
            var (anyOnline, lastSeen) = await GetSupportPresenceAsync();
            var site = await _context.SiteSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
            return new ChatPeerDto
            {
                Conversation = conversation,
                Title = string.IsNullOrWhiteSpace(site?.SiteName) ? SiteDefaults.Name : site!.SiteName,
                AvatarUrl = string.IsNullOrEmpty(site?.LogoImage) ? null : "/uploads/site/" + site!.LogoImage,
                Online = anyOnline,
                LastSeen = lastSeen,
                ReadUpToId = conv?.AdminReadId ?? 0,
                DeliveredUpToId = conv?.AdminDeliveredId ?? 0
            };
        }

        private async Task<ChatPeerDto> GetGroupPeerAsync(int groupId, string viewer, bool viewerIsAdmin)
        {
            var g = await _context.ChatGroups.AsNoTracking().FirstAsync(x => x.Id == groupId);
            var ptr = (await MinPointersAsync(new List<int> { groupId })).GetValueOrDefault(groupId);

            if (g.IsDirect)
            {
                // Şəxsi yazışma: başlıqda qarşı tərəf (ad, şəkil, onlayn)
                var other = (await DirectPeersAsync(new List<int> { groupId }, viewer)).GetValueOrDefault(groupId);
                return new ChatPeerDto
                {
                    Conversation = g.Key,
                    IsDirect = true,
                    PeerUserName = other?.UserName,
                    Title = other?.Title ?? L.T("İstifadəçi"),
                    AvatarUrl = other?.AvatarUrl,
                    Online = other?.Online ?? false,
                    LastSeen = other?.LastSeen,
                    CanWrite = other != null,
                    ReadUpToId = ptr.Read,
                    DeliveredUpToId = ptr.Delivered
                };
            }
            var members = g.AllUsers
                ? await _context.Users.CountAsync(u => u.EmailConfirmed)
                : await _context.ChatGroupMembers.CountAsync(m => m.GroupId == groupId && m.IsExplicit);

            return new ChatPeerDto
            {
                Conversation = g.Key,
                Title = g.Name,
                IsGroup = true,
                MemberCount = members,
                AdminsOnly = g.AdminsOnly,
                AllUsers = g.AllUsers,
                CanWrite = viewerIsAdmin || !g.AdminsOnly,
                ReadUpToId = ptr.Read,
                DeliveredUpToId = ptr.Delivered
            };
        }

        private async Task<List<(bool Online, DateTime? LastSeen)>> AdminPresenceAsync()
        {
            var adminRole = nameof(Roles.Admin);
            var list = await (
                from u in _context.Users
                join ur in _context.UserRoles on u.Id equals ur.UserId
                join r in _context.Roles on ur.RoleId equals r.Id
                where r.Name == adminRole
                select new { u.ChatOnline, u.ChatLastSeen }
            ).AsNoTracking().ToListAsync();
            return list.Select(x => (x.ChatOnline, x.ChatLastSeen)).ToList();
        }

        private async Task<HashSet<string>> AdminNamesAsync()
        {
            var adminRole = nameof(Roles.Admin);
            var list = await (
                from u in _context.Users
                join ur in _context.UserRoles on u.Id equals ur.UserId
                join r in _context.Roles on ur.RoleId equals r.Id
                where r.Name == adminRole
                select u.UserName!
            ).ToListAsync();
            return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }

        // ================= GÖNDƏR =================

        public async Task<ChatResult> SendAsync(ChatSendVM vm, string conversation, string username, bool isAdmin)
        {
            var text = Normalize(vm.Message);

            if (text == null && vm.File == null)
                return ChatResult.Invalid(L.T("Mesaj və ya fayl əlavə edin."));

            if (text != null && text.Length > MaxMessageLength)
                return ChatResult.Invalid(L.T("Mesaj maksimum {0} simvol ola bilər.", MaxMessageLength));

            ChatGroup? group = null;
            if (GroupIdOf(conversation) is int gid)
            {
                group = await _context.ChatGroups.FirstOrDefaultAsync(g => g.Id == gid);
                if (group == null) return ChatResult.Invalid(L.T("Yazışma tapılmadı."));
                if (group.AdminsOnly && !isAdmin) return ChatResult.Invalid(L.T("Bu qrupda yalnız ustalar yaza bilər."));
            }

            // Cavab yalnız eyni yazışmadakı mesaja ola bilər
            int? replyTo = null;
            if (vm.ReplyToId is int rid && rid > 0 &&
                await _context.ChatMessages.AnyAsync(m => m.Id == rid && !m.SoftDeleted && m.ConversationUserName == conversation))
                replyTo = rid;

            string? url = null, type = null;
            if (vm.File != null)
            {
                var saved = await SaveFileAsync(vm.File);
                if (saved.Error != null)
                    return ChatResult.Invalid(saved.Error);
                (url, type) = (saved.Url, saved.Type);
            }

            var now = AzTime.Now;
            var msg = new ChatMessage
            {
                Message = text,
                UserName = username,
                ConversationUserName = conversation,
                SenderIsAdmin = isAdmin,
                ReplyToId = replyTo,
                AttachmentUrl = url,
                AttachmentType = type,
                CreatedDate = now
            };

            try
            {
                await _context.ChatMessages.AddAsync(msg);
                await _context.SaveChangesAsync();

                if (group != null)
                {
                    group.LastMessageId = msg.Id;
                    group.LastMessageDate = now;
                    var me = await GetOrCreateMemberAsync(group.Id, username, explicitMember: false);
                    me.ReadId = Math.Max(me.ReadId, msg.Id);
                    me.DeliveredId = Math.Max(me.DeliveredId, msg.Id);
                }
                else
                {
                    // Yazışmanın vəziyyəti: son mesaj; göndərən tərəf öz mesajını "oxumuş" sayılır
                    var conv = await GetOrCreateConversationAsync(conversation);
                    conv.LastMessageId = msg.Id;
                    conv.LastMessageDate = now;
                    if (isAdmin)
                    {
                        conv.AdminReadId = Math.Max(conv.AdminReadId, msg.Id);
                        conv.AdminDeliveredId = Math.Max(conv.AdminDeliveredId, msg.Id);
                    }
                    else
                    {
                        conv.UserReadId = Math.Max(conv.UserReadId, msg.Id);
                        conv.UserDeliveredId = Math.Max(conv.UserDeliveredId, msg.Id);
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch
            {
                DeleteFile(url); // bazaya yazılmadısa faylı yetim qoyma
                throw;
            }

            return ChatResult.Ok((await MapAsync(new List<ChatMessage> { msg }))[0]);
        }

        private async Task<ChatConversation> GetOrCreateConversationAsync(string conversation)
        {
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.UserName == conversation);
            if (conv != null) return conv;

            conv = new ChatConversation { UserName = conversation };
            _context.ChatConversations.Add(conv);
            return conv;
        }

        /// <summary>Qrup üzvünün sətri (✓✓ üçün). "Bütün istifadəçilər" qrupunda ilk açılışda yaradılır.</summary>
        private async Task<ChatGroupMember> GetOrCreateMemberAsync(int groupId, string userName, bool explicitMember)
        {
            var m = await _context.ChatGroupMembers.FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserName == userName);
            if (m != null)
            {
                if (explicitMember) m.IsExplicit = true;
                return m;
            }
            m = new ChatGroupMember { GroupId = groupId, UserName = userName, IsExplicit = explicitMember, JoinedDate = AzTime.Now };
            _context.ChatGroupMembers.Add(m);
            return m;
        }

        // ================= REDAKTƏ =================

        public async Task<ChatResult> EditAsync(ChatEditVM vm, string username)
        {
            var msg = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == vm.Id && !m.SoftDeleted);

            // Başqasının mesajı da "tapılmadı" kimi qaytarılır
            if (msg == null || msg.UserName != username)
                return ChatResult.NotFound();

            var text = Normalize(vm.Message);
            if (text != null && text.Length > MaxMessageLength)
                return ChatResult.Invalid(L.T("Mesaj maksimum {0} simvol ola bilər.", MaxMessageLength));

            var keepsAttachment = msg.AttachmentUrl != null && !vm.RemoveAttachment && vm.File == null;
            if (text == null && vm.File == null && !keepsAttachment)
                return ChatResult.Invalid(L.T("Mesaj boş qala bilməz. Mətn yazın və ya fayl əlavə edin."));

            string? oldFile = null;

            if (vm.File != null)
            {
                var saved = await SaveFileAsync(vm.File);
                if (saved.Error != null)
                    return ChatResult.Invalid(saved.Error);

                oldFile = msg.AttachmentUrl;
                msg.AttachmentUrl = saved.Url;
                msg.AttachmentType = saved.Type;
            }
            else if (vm.RemoveAttachment && msg.AttachmentUrl != null)
            {
                oldFile = msg.AttachmentUrl;
                msg.AttachmentUrl = null;
                msg.AttachmentType = null;
            }

            msg.Message = text;
            msg.IsEdited = true;
            msg.EditedDate = AzTime.Now;

            await _context.SaveChangesAsync();

            // Köhnə fayl yalnız baza uğurla yeniləndikdən sonra silinir
            await DeleteFileIfUnusedAsync(oldFile);

            return ChatResult.Ok((await MapAsync(new List<ChatMessage> { msg }))[0]);
        }

        // ================= SİL =================

        public async Task<string?> DeleteAsync(int id, string username, bool isAdmin)
        {
            var msg = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == id && !m.SoftDeleted);

            if (msg == null || (msg.UserName != username && !isAdmin))
                return null;
            if (msg.UserName != username && IsDirectKey(msg.ConversationUserName) &&
                await ResolveConversationAsync(username, isAdmin, msg.ConversationUserName) == null)
                return null;

            var conversation = msg.ConversationUserName ?? "";
            var file = msg.AttachmentUrl;
            _context.ChatMessages.Remove(msg);
            await _context.ChatReactions.Where(r => r.MessageId == id).ExecuteDeleteAsync();
            await _context.SaveChangesAsync();

            // Son mesaj silinibsə — siyahıda əvvəlki mesaj görünsün
            var prev = await _context.ChatMessages.AsNoTracking()
                .Where(m => !m.SoftDeleted && m.ConversationUserName == conversation)
                .OrderByDescending(m => m.Id)
                .Select(m => new { m.Id, m.CreatedDate })
                .FirstOrDefaultAsync();

            if (GroupIdOf(conversation) is int gid)
            {
                var g = await _context.ChatGroups.FirstOrDefaultAsync(x => x.Id == gid);
                if (g != null && g.LastMessageId == id)
                {
                    g.LastMessageId = prev?.Id ?? 0;
                    g.LastMessageDate = prev?.CreatedDate ?? g.CreatedDate;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.UserName == conversation);
                if (conv != null && conv.LastMessageId == id)
                {
                    if (prev == null)
                        _context.ChatConversations.Remove(conv);
                    else
                    {
                        conv.LastMessageId = prev.Id;
                        conv.LastMessageDate = prev.CreatedDate;
                    }
                    await _context.SaveChangesAsync();
                }
            }

            await DeleteFileIfUnusedAsync(file);
            return conversation;
        }

        // ================= REAKSİYA =================

        public async Task<ChatMessageDto?> ReactAsync(int id, string? emoji, string username, bool isAdmin)
        {
            var msg = await _context.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && !m.SoftDeleted);
            if (msg == null || await ResolveConversationAsync(username, isAdmin, msg.ConversationUserName) == null)
                return null;

            emoji = string.IsNullOrWhiteSpace(emoji) ? null : emoji.Trim();
            if (emoji != null && !AllowedReactions.Contains(emoji))
                return null;

            var existing = await _context.ChatReactions.FirstOrDefaultAsync(r => r.MessageId == id && r.UserName == username);
            if (existing != null && (emoji == null || existing.Emoji == emoji))
                _context.ChatReactions.Remove(existing);          // götür
            else if (existing != null)
                existing.Emoji = emoji!;                            // dəyiş
            else if (emoji != null)
                _context.ChatReactions.Add(new ChatReaction { MessageId = id, UserName = username, Emoji = emoji, CreatedDate = AzTime.Now });

            await _context.SaveChangesAsync();
            return (await MapAsync(new List<ChatMessage> { msg }))[0];
        }

        // ================= OXUNMAMIŞ / ✓✓ =================

        public async Task<int> GetUnreadCountAsync(string username, bool isAdmin)
        {
            int total;
            if (isAdmin)
            {
                total = await (
                    from m in _context.ChatMessages
                    join c in _context.ChatConversations on m.ConversationUserName equals c.UserName
                    where !m.SoftDeleted && !m.SenderIsAdmin && m.Id > c.AdminReadId
                    select m.Id
                ).CountAsync();
            }
            else
            {
                var readId = await _context.ChatConversations.AsNoTracking()
                    .Where(c => c.UserName == username)
                    .Select(c => (int?)c.UserReadId)
                    .FirstOrDefaultAsync();
                total = readId == null ? 0 : await _context.ChatMessages
                    .CountAsync(m => !m.SoftDeleted && m.ConversationUserName == username && m.SenderIsAdmin && m.Id > readId.Value);
            }

            // "Mənim üçün silinmiş" yazışmalar: silinmədən əvvəlki oxunmamışlar sayılmır
            var myClears = await _context.ChatClears.AsNoTracking().Where(c => c.UserName == username).ToListAsync();
            foreach (var cl in myClears.Where(x => !IsGroupKey(x.Conversation)))
            {
                var before = isAdmin
                    ? await (from m in _context.ChatMessages
                             join c in _context.ChatConversations on m.ConversationUserName equals c.UserName
                             where c.UserName == cl.Conversation && !m.SoftDeleted && !m.SenderIsAdmin && m.Id > c.AdminReadId && m.Id <= cl.ClearedId
                             select m.Id).CountAsync()
                    : cl.Conversation == username
                        ? await (from m in _context.ChatMessages
                                 join c in _context.ChatConversations on m.ConversationUserName equals c.UserName
                                 where c.UserName == username && !m.SoftDeleted && m.SenderIsAdmin && m.Id > c.UserReadId && m.Id <= cl.ClearedId
                                 select m.Id).CountAsync()
                        : 0;
                total -= before;
            }
            var groupClears = myClears.Where(x => IsGroupKey(x.Conversation)).ToDictionary(x => x.Conversation, x => x.ClearedId);

            // Qruplar
            var groups = await AccessibleGroups(username, isAdmin).AsNoTracking()
                .Select(g => new
                {
                    Key = (g.IsDirect ? ChatGroup.DirectPrefix : ChatGroup.KeyPrefix) + g.Id,
                    ReadId = _context.ChatGroupMembers.Where(m => m.GroupId == g.Id && m.UserName == username).Select(m => (int?)m.ReadId).FirstOrDefault() ?? 0
                })
                .ToListAsync();
            foreach (var g in groups)
            {
                var from = Math.Max(g.ReadId, groupClears.GetValueOrDefault(g.Key));
                total += await _context.ChatMessages.CountAsync(m => !m.SoftDeleted && m.ConversationUserName == g.Key && m.Id > from && m.UserName != username);
            }

            return Math.Max(0, total);
        }

        public Task<ChatReceiptDto?> MarkReadAsync(string conversation, string reader, bool readerIsAdmin, int lastId) =>
            UpdateReceiptAsync(conversation, reader, readerIsAdmin, lastId, read: true);

        public Task<ChatReceiptDto?> MarkDeliveredAsync(string conversation, string reader, bool readerIsAdmin, int lastId) =>
            UpdateReceiptAsync(conversation, reader, readerIsAdmin, lastId, read: false);

        private async Task<ChatReceiptDto?> UpdateReceiptAsync(string conversation, string reader, bool readerIsAdmin, int lastId, bool read)
        {
            if (lastId <= 0) return null;

            if (GroupIdOf(conversation) is int gid)
            {
                var g = await _context.ChatGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gid);
                if (g == null) return null;
                var target = Math.Min(lastId, g.LastMessageId);
                var me = await GetOrCreateMemberAsync(gid, reader, explicitMember: false);
                var before = (me.ReadId, me.DeliveredId);
                if (read) me.ReadId = Math.Max(me.ReadId, target);
                me.DeliveredId = Math.Max(me.DeliveredId, Math.Max(me.ReadId, target));
                if (before == (me.ReadId, me.DeliveredId) && me.Id != 0) return null;
                await _context.SaveChangesAsync();

                var ptr = (await MinPointersAsync(new List<int> { gid })).GetValueOrDefault(gid);
                return new ChatReceiptDto { Conversation = g.Key, Side = "group", ReadUpToId = ptr.Read, DeliveredUpToId = ptr.Delivered };
            }

            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.UserName == conversation);
            if (conv == null) return null;

            // Mövcud olmayan gələcək Id yazılmasın
            var t = Math.Min(lastId, conv.LastMessageId);
            var changed = false;

            if (readerIsAdmin)
            {
                if (read && t > conv.AdminReadId) { conv.AdminReadId = t; changed = true; }
                var delivered = Math.Max(conv.AdminReadId, t);
                if (delivered > conv.AdminDeliveredId) { conv.AdminDeliveredId = delivered; changed = true; }
            }
            else
            {
                if (read && t > conv.UserReadId) { conv.UserReadId = t; changed = true; }
                var delivered = Math.Max(conv.UserReadId, t);
                if (delivered > conv.UserDeliveredId) { conv.UserDeliveredId = delivered; changed = true; }
            }

            if (!changed) return null;
            await _context.SaveChangesAsync();

            return new ChatReceiptDto
            {
                Conversation = conversation,
                Side = readerIsAdmin ? "admin" : "user",
                ReadUpToId = readerIsAdmin ? conv.AdminReadId : conv.UserReadId,
                DeliveredUpToId = readerIsAdmin ? conv.AdminDeliveredId : conv.UserDeliveredId
            };
        }

        public async Task<List<ChatReceiptDto>> MarkAllDeliveredAsync(string username, bool isAdmin)
        {
            var result = new List<ChatReceiptDto>();

            // Şəxsi yazışmalar: müştəri — öz yazışması; usta — bütün yazışmalar (ustalar tərəfi ortaqdır)
            var convs = isAdmin
                ? await _context.ChatConversations.Where(c => c.AdminDeliveredId < c.LastMessageId).Take(500).ToListAsync()
                : await _context.ChatConversations.Where(c => c.UserName == username && c.UserDeliveredId < c.LastMessageId).ToListAsync();
            foreach (var c in convs)
            {
                if (isAdmin) c.AdminDeliveredId = c.LastMessageId;
                else c.UserDeliveredId = c.LastMessageId;
                result.Add(new ChatReceiptDto
                {
                    Conversation = c.UserName,
                    Side = isAdmin ? "admin" : "user",
                    ReadUpToId = isAdmin ? c.AdminReadId : c.UserReadId,
                    DeliveredUpToId = isAdmin ? c.AdminDeliveredId : c.UserDeliveredId
                });
            }

            // Qruplar
            var groups = await AccessibleGroups(username, isAdmin).Where(g => g.LastMessageId > 0).AsNoTracking()
                .Select(g => new { g.Id, g.LastMessageId, g.IsDirect }).ToListAsync();
            var directIds = groups.Where(g => g.IsDirect).Select(g => g.Id).ToHashSet();
            var changedGroups = new List<int>();
            foreach (var g in groups)
            {
                var m = await GetOrCreateMemberAsync(g.Id, username, explicitMember: false);
                if (m.DeliveredId >= g.LastMessageId) continue;
                m.DeliveredId = g.LastMessageId;
                changedGroups.Add(g.Id);
            }

            if (convs.Count == 0 && changedGroups.Count == 0) return result;
            await _context.SaveChangesAsync();

            if (changedGroups.Count > 0)
            {
                var ptrs = await MinPointersAsync(changedGroups);
                foreach (var id in changedGroups)
                {
                    var p = ptrs.GetValueOrDefault(id);
                    result.Add(new ChatReceiptDto { Conversation = (directIds.Contains(id) ? ChatGroup.DirectPrefix : ChatGroup.KeyPrefix) + id, Side = "group", ReadUpToId = p.Read, DeliveredUpToId = p.Delivered });
                }
            }
            return result;
        }

        // ================= ONLAYN =================

        public async Task SetPresenceAsync(string username, bool online)
        {
            await _context.Users
                .Where(u => u.UserName == username)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.ChatOnline, online)
                    .SetProperty(u => u.ChatLastSeen, AzTime.Now));
        }

        public async Task<(bool Online, string? LastSeen)> GetSupportPresenceAsync()
        {
            var admins = await AdminPresenceAsync();
            var now = AzTime.Now;
            var online = admins.Any(a => IsOnline(a.Online, a.LastSeen, now));
            return (online, online ? null : Iso(admins.Max(a => a.LastSeen)));
        }

        private static bool IsOnline(bool flag, DateTime? lastSeen, DateTime now) =>
            flag && lastSeen.HasValue && now - lastSeen.Value < OnlineWindow;

        // ================= QRUPLAR =================

        public async Task<List<ChatUserDto>> GetUsersAsync(string? query)
        {
            var q = _context.Users.AsNoTracking().Where(u => u.EmailConfirmed);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim();
                q = q.Where(u => u.UserName!.Contains(s) || u.FullName.Contains(s) || u.Email!.Contains(s));
            }
            var users = await q.OrderBy(u => u.FullName).Take(300)
                .Select(u => new { u.UserName, u.FullName, u.ProfileImage, u.ChatOnline, u.ChatLastSeen })
                .ToListAsync();
            var admins = await AdminNamesAsync();
            var now = AzTime.Now;
            return users.Select(u => new ChatUserDto
            {
                UserName = u.UserName!,
                FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : u.FullName,
                AvatarUrl = AvatarUrl(u.ProfileImage),
                IsAdmin = admins.Contains(u.UserName!),
                Online = IsOnline(u.ChatOnline, u.ChatLastSeen, now)
            }).ToList();
        }

        public async Task<(ChatGroupInfoDto? Group, string? Error)> CreateGroupAsync(ChatGroupSaveVM vm, string creator)
        {
            var name = (vm.Name ?? "").Trim();
            if (name.Length == 0) return (null, L.T("Qrupun adını yazın."));
            if (name.Length > 100) return (null, L.T("Qrupun adı maksimum 100 simvol ola bilər."));

            var members = await ValidUserNamesAsync(vm.Members);
            if (!vm.AllUsers && members.Count == 0) return (null, L.T("Ən azı bir üzv seçin və ya \"Bütün istifadəçilər\" seçimini işarələyin."));

            var now = AzTime.Now;
            var group = new ChatGroup
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                AllUsers = vm.AllUsers,
                AdminsOnly = vm.AdminsOnly,
                CreatedBy = creator,
                CreatedDate = now,
                LastMessageDate = now
            };
            _context.ChatGroups.Add(group);
            await _context.SaveChangesAsync();

            foreach (var u in members.Append(creator).Distinct(StringComparer.OrdinalIgnoreCase))
                _context.ChatGroupMembers.Add(new ChatGroupMember { GroupId = group.Id, UserName = u, IsExplicit = true, JoinedDate = now });
            await _context.SaveChangesAsync();

            return (await GetGroupAsync(group.Id, creator, true), null);
        }

        public async Task<(ChatGroupInfoDto? Group, string? Error)> UpdateGroupAsync(int id, ChatGroupSaveVM vm)
        {
            var group = await _context.ChatGroups.FirstOrDefaultAsync(g => g.Id == id && !g.IsDirect);
            if (group == null) return (null, L.T("Yazışma tapılmadı."));
            var name = (vm.Name ?? "").Trim();
            if (name.Length == 0) return (null, L.T("Qrupun adını yazın."));

            group.Name = name.Length > 100 ? name[..100] : name;
            group.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            group.AllUsers = vm.AllUsers;
            group.AdminsOnly = vm.AdminsOnly;

            // Üzvlər: göndərilibsə — əlavə olunur (çıxarmaq ayrıca əməliyyatdır)
            var now = AzTime.Now;
            foreach (var u in await ValidUserNamesAsync(vm.Members))
            {
                var m = await GetOrCreateMemberAsync(id, u, explicitMember: true);
                if (m.Id == 0) m.JoinedDate = now;
            }
            await _context.SaveChangesAsync();
            return (await GetGroupAsync(id, "", true), null);
        }

        public async Task<ChatGroupInfoDto?> GetGroupAsync(int id, string viewer, bool isAdmin)
        {
            if (!await CanAccessGroupAsync(id, viewer, isAdmin)) return null;
            var g = await _context.ChatGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (g == null || g.IsDirect) return null;

            var names = await _context.ChatGroupMembers.AsNoTracking()
                .Where(m => m.GroupId == id && m.IsExplicit).Select(m => m.UserName).ToListAsync();
            var users = await _context.Users.AsNoTracking().Where(u => names.Contains(u.UserName!))
                .Select(u => new { u.UserName, u.FullName, u.ProfileImage, u.ChatOnline, u.ChatLastSeen }).ToListAsync();
            var admins = await AdminNamesAsync();
            var now = AzTime.Now;

            return new ChatGroupInfoDto
            {
                Conversation = g.Key,
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                AllUsers = g.AllUsers,
                AdminsOnly = g.AdminsOnly,
                CreatedBy = g.CreatedBy,
                CreatedDate = Iso(g.CreatedDate) ?? "",
                MemberCount = g.AllUsers ? await _context.Users.CountAsync(u => u.EmailConfirmed) : users.Count,
                Members = users.Select(u => new ChatUserDto
                {
                    UserName = u.UserName!,
                    FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : u.FullName,
                    AvatarUrl = AvatarUrl(u.ProfileImage),
                    IsAdmin = admins.Contains(u.UserName!),
                    Online = IsOnline(u.ChatOnline, u.ChatLastSeen, now)
                }).OrderByDescending(u => u.IsAdmin).ThenBy(u => u.FullName).ToList()
            };
        }

        public async Task<ChatAudience?> DeleteGroupAsync(int id)
        {
            var group = await _context.ChatGroups.FirstOrDefaultAsync(g => g.Id == id && !g.IsDirect);
            if (group == null) return null;
            var audience = await GetAudienceAsync(group.Key);

            var key = group.Key;
            var files = await _context.ChatMessages.Where(m => m.ConversationUserName == key && m.AttachmentUrl != null)
                .Select(m => m.AttachmentUrl!).ToListAsync();
            var ids = _context.ChatMessages.Where(m => m.ConversationUserName == key).Select(m => m.Id);
            await _context.ChatReactions.Where(r => ids.Contains(r.MessageId)).ExecuteDeleteAsync();
            await _context.ChatMessages.Where(m => m.ConversationUserName == key).ExecuteDeleteAsync();
            await _context.ChatGroupMembers.Where(m => m.GroupId == id).ExecuteDeleteAsync();
            _context.ChatGroups.Remove(group);
            await _context.SaveChangesAsync();

            foreach (var f in files.Distinct()) await DeleteFileIfUnusedAsync(f);
            return audience;
        }

        public async Task<ChatGroupInfoDto?> RemoveMemberAsync(int id, string userName)
        {
            if (await _context.ChatGroups.AnyAsync(g => g.Id == id && g.IsDirect)) return null;
            var m = await _context.ChatGroupMembers.FirstOrDefaultAsync(x => x.GroupId == id && x.UserName == userName);
            if (m != null)
            {
                _context.ChatGroupMembers.Remove(m);
                await _context.SaveChangesAsync();
            }
            return await GetGroupAsync(id, "", true);
        }

        private async Task<List<string>> ValidUserNamesAsync(List<string>? names)
        {
            var wanted = (names ?? new()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().Take(1000).ToList();
            if (wanted.Count == 0) return new();
            return await _context.Users.AsNoTracking()
                .Where(u => wanted.Contains(u.UserName!))
                .Select(u => u.UserName!).ToListAsync();
        }

        // ================= PROFİL VƏ MEDİA =================

        public const long MaxAvatarBytes = 5L * 1024 * 1024;   // saytla eyni

        public async Task<ChatProfileDto?> GetProfileAsync(string username)
        {
            var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == username);
            return u == null ? null : new ChatProfileDto
            {
                UserName = u.UserName!,
                FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : u.FullName,
                Email = u.Email,
                AvatarUrl = AvatarUrl(u.ProfileImage),
                About = u.ChatAbout
            };
        }

        public async Task<(ChatProfileDto? Profile, string? Error)> UpdateProfileAsync(string username, ChatProfileSaveVM vm)
        {
            var name = (vm.FullName ?? "").Trim();
            if (name.Length == 0) return (null, L.T("Adınızı yazın."));
            if (name.Length > 100) return (null, L.T("Ad maksimum 100 simvol ola bilər."));
            var about = string.IsNullOrWhiteSpace(vm.About) ? null : vm.About.Trim();
            if (about != null && about.Length > 140) return (null, L.T("\"Haqqında\" maksimum 140 simvol ola bilər."));

            var u = await _context.Users.FirstOrDefaultAsync(x => x.UserName == username);
            if (u == null) return (null, L.T("İstifadəçi tapılmadı."));
            u.FullName = name;
            u.ChatAbout = about;
            await _context.SaveChangesAsync();
            return (await GetProfileAsync(username), null);
        }

        public async Task<(ChatProfileDto? Profile, string? Error)> SetAvatarAsync(string username, IFormFile? file)
        {
            var u = await _context.Users.FirstOrDefaultAsync(x => x.UserName == username);
            if (u == null) return (null, L.T("İstifadəçi tapılmadı."));

            string? newName = null;
            if (file != null)
            {
                // Uzantı, MIME, ölçü və həqiqi məzmun (magic bytes) yoxlanılır — saytdakı kimi
                if (!AllowedUploadAttribute.IsAllowed(file, UploadKind.Image, out var error, MaxAvatarBytes))
                    return (null, error);
                var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
                Directory.CreateDirectory(folder);
                newName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
                await using var stream = new FileStream(Path.Combine(folder, newName), FileMode.CreateNew);
                await file.CopyToAsync(stream);
            }

            var old = u.ProfileImage;
            u.ProfileImage = newName;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(old))
            {
                try
                {
                    var path = Path.Combine(_env.WebRootPath, "uploads", "avatars", Path.GetFileName(old));
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Köhnə profil şəkli silinmədi: {File}", old); }
            }
            return (await GetProfileAsync(username), null);
        }

        public async Task<ChatContactDto?> GetContactAsync(string conversation, string viewer, bool viewerIsAdmin)
        {
            var contact = new ChatContactDto { Conversation = conversation };
            var now = AzTime.Now;

            if (IsDirectKey(conversation) && GroupIdOf(conversation) is int did)
            {
                // Şəxsi yazışma: qarşı tərəfin profili (email / telefon yalnız ustaya)
                var other = (await DirectPeersAsync(new List<int> { did }, viewer)).GetValueOrDefault(did);
                if (other == null) return null;
                var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == other.UserName);
                contact.IsDirect = true;
                contact.PeerUserName = other.UserName;
                contact.UserName = other.UserName;
                contact.Title = other.Title;
                contact.AvatarUrl = other.AvatarUrl;
                contact.About = u?.ChatAbout;
                contact.Online = other.Online;
                contact.LastSeen = other.LastSeen;
                if (viewerIsAdmin)
                {
                    contact.Email = u?.Email;
                    contact.Phone = u?.PhoneNumber;
                }
            }
            else if (GroupIdOf(conversation) is int gid)
            {
                var g = await _context.ChatGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gid);
                if (g == null) return null;
                contact.IsGroup = true;
                contact.Title = g.Name;
                contact.About = g.Description;
            }
            else if (!viewerIsAdmin)
            {
                // Müştəri üçün qarşı tərəf — ustalar (sayt)
                var site = await _context.SiteSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
                var (online, lastSeen) = await GetSupportPresenceAsync();
                contact.IsSupport = true;
                contact.Title = string.IsNullOrWhiteSpace(site?.SiteName) ? SiteDefaults.Name : site!.SiteName;
                contact.AvatarUrl = string.IsNullOrEmpty(site?.LogoImage) ? null : "/uploads/site/" + site!.LogoImage;
                contact.About = L.T("Canlı usta dəstəyi");
                contact.Online = online;
                contact.LastSeen = lastSeen;
            }
            else
            {
                var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserName == conversation);
                if (u == null) return null;
                var online = IsOnline(u.ChatOnline, u.ChatLastSeen, now);
                contact.Title = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : u.FullName;
                contact.UserName = u.UserName;
                contact.AvatarUrl = AvatarUrl(u.ProfileImage);
                contact.About = u.ChatAbout;
                contact.Online = online;
                contact.LastSeen = online ? null : Iso(u.ChatLastSeen);
                contact.Email = u.Email;
                contact.Phone = u.PhoneNumber;
            }

            var clearedId = await ClearedIdAsync(viewer, conversation);
            var q = _context.ChatMessages.AsNoTracking().Where(m => !m.SoftDeleted && m.ConversationUserName == conversation && m.Id > clearedId);
            contact.MediaCount = await q.CountAsync(m => m.AttachmentType == "image" || m.AttachmentType == "video");
            contact.AudioCount = await q.CountAsync(m => m.AttachmentType == "audio");
            contact.LinkCount = await q.CountAsync(m => m.Message != null && (m.Message.Contains("http://") || m.Message.Contains("https://")));
            return contact;
        }

        public async Task<ChatPageDto> GetMediaAsync(string conversation, string viewer, string kind, int? beforeId, int take)
        {
            take = Math.Clamp(take, 1, 120);
            var cleared = await ClearedIdAsync(viewer, conversation);
            var q = _context.ChatMessages.AsNoTracking().Where(m => !m.SoftDeleted && m.ConversationUserName == conversation && m.Id > cleared);
            q = kind switch
            {
                "audio" => q.Where(m => m.AttachmentType == "audio"),
                "links" => q.Where(m => m.Message != null && (m.Message.Contains("http://") || m.Message.Contains("https://"))),
                _ => q.Where(m => m.AttachmentType == "image" || m.AttachmentType == "video")
            };
            if (beforeId.HasValue) q = q.Where(m => m.Id < beforeId.Value);
            var items = await q.OrderByDescending(m => m.Id).Take(take + 1).ToListAsync();
            return new ChatPageDto { Messages = await MapAsync(items.Take(take).ToList()), HasMore = items.Count > take };
        }

        // ================= YAZIŞMANI SİL =================

        private async Task<int> ClearedIdAsync(string user, string conversation) =>
            await _context.ChatClears.AsNoTracking()
                .Where(c => c.UserName == user && c.Conversation == conversation)
                .Select(c => (int?)c.ClearedId).FirstOrDefaultAsync() ?? 0;

        /// <summary>Silinmədən sonra gələn oxunmamış mesajlar.</summary>
        private async Task<int> CountUnreadAfterAsync(string conversation, string viewer, bool isAdmin, int cleared)
        {
            if (GroupIdOf(conversation) is int gid)
            {
                var readId = await _context.ChatGroupMembers.Where(m => m.GroupId == gid && m.UserName == viewer)
                    .Select(m => (int?)m.ReadId).FirstOrDefaultAsync() ?? 0;
                var from = Math.Max(readId, cleared);
                return await _context.ChatMessages.CountAsync(m => !m.SoftDeleted && m.ConversationUserName == conversation && m.Id > from && m.UserName != viewer);
            }
            var conv = await _context.ChatConversations.AsNoTracking().FirstOrDefaultAsync(c => c.UserName == conversation);
            if (conv == null) return 0;
            var after = Math.Max(isAdmin ? conv.AdminReadId : conv.UserReadId, cleared);
            return await _context.ChatMessages.CountAsync(m => !m.SoftDeleted && m.ConversationUserName == conversation &&
                                                               m.Id > after && m.SenderIsAdmin != isAdmin);
        }

        public async Task<(bool Ok, string? Error)> ClearConversationAsync(string conversation, string username, bool isAdmin, bool forEveryone)
        {
            if (forEveryone && !isAdmin)
                return (false, L.T("Hamı üçün silməyə yalnız ustaların icazəsi var."));

            if (!forEveryone)
            {
                // Yalnız mənim üçün: indiki son mesaja qədər gizlət
                var lastId = await _context.ChatMessages.Where(m => m.ConversationUserName == conversation)
                    .MaxAsync(m => (int?)m.Id) ?? 0;
                var row = await _context.ChatClears.FirstOrDefaultAsync(c => c.UserName == username && c.Conversation == conversation);
                if (row == null)
                {
                    row = new ChatClear { UserName = username, Conversation = conversation };
                    _context.ChatClears.Add(row);
                }
                row.ClearedId = Math.Max(row.ClearedId, lastId);
                row.ClearedDate = AzTime.Now;
                await _context.SaveChangesAsync();
                return (true, null);
            }

            // Hamı üçün (usta): mesajlar, reaksiyalar və fayllar silinir
            var files = await _context.ChatMessages.Where(m => m.ConversationUserName == conversation && m.AttachmentUrl != null)
                .Select(m => m.AttachmentUrl!).Distinct().ToListAsync();
            var ids = _context.ChatMessages.Where(m => m.ConversationUserName == conversation).Select(m => m.Id);
            await _context.ChatReactions.Where(r => ids.Contains(r.MessageId)).ExecuteDeleteAsync();
            await _context.ChatMessages.Where(m => m.ConversationUserName == conversation).ExecuteDeleteAsync();
            await _context.ChatClears.Where(c => c.Conversation == conversation).ExecuteDeleteAsync();

            if (GroupIdOf(conversation) is int gid)
            {
                await _context.ChatGroups.Where(g => g.Id == gid)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.LastMessageId, 0));
                await _context.ChatGroupMembers.Where(m => m.GroupId == gid)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadId, 0).SetProperty(m => m.DeliveredId, 0));
            }
            else
            {
                await _context.ChatConversations.Where(c => c.UserName == conversation).ExecuteDeleteAsync();
            }

            foreach (var f in files) await DeleteFileIfUnusedAsync(f);
            return (true, null);
        }

        // ================= YENİ ÇAT ("+") =================

        public async Task<List<ChatUserDto>> GetPeopleAsync(string viewer, string? query)
        {
            var q = _context.Users.AsNoTracking().Where(u => u.EmailConfirmed && u.UserName != viewer);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim();
                if (s.Length > 100) s = s[..100];
                q = q.Where(u => u.UserName!.Contains(s) || u.FullName.Contains(s));   // email ilə axtarış yoxdur (məxfilik)
            }
            var users = await q.OrderBy(u => u.FullName).ThenBy(u => u.UserName).Take(200)
                .Select(u => new { u.UserName, u.FullName, u.ProfileImage, u.ChatOnline, u.ChatLastSeen })
                .ToListAsync();
            var admins = await AdminNamesAsync();
            var now = AzTime.Now;
            return users.Select(u => new ChatUserDto
            {
                UserName = u.UserName!,
                FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName! : u.FullName,
                AvatarUrl = AvatarUrl(u.ProfileImage),
                IsAdmin = admins.Contains(u.UserName!),
                Online = IsOnline(u.ChatOnline, u.ChatLastSeen, now)
            })
            // Əvvəl ustalar, sonra onlayn olanlar
            .OrderByDescending(u => u.IsAdmin).ThenByDescending(u => u.Online).ThenBy(u => u.FullName)
            .ToList();
        }

        public async Task<(string? Conversation, string? Error)> StartDirectAsync(string me, bool meIsAdmin, string userName)
        {
            var normalized = (userName ?? "").Trim().ToUpperInvariant();
            if (normalized.Length == 0) return (null, L.T("İstifadəçi tapılmadı."));
            var other = await _context.Users.AsNoTracking()
                .Where(u => u.NormalizedUserName == normalized && u.EmailConfirmed)
                .Select(u => u.UserName).FirstOrDefaultAsync();
            if (other == null) return (null, L.T("İstifadəçi tapılmadı."));
            if (string.Equals(other, me, StringComparison.OrdinalIgnoreCase)) return (null, L.T("Özünüzə yaza bilməzsiniz."));

            // Müştəri ↔ usta: mövcud "ustalarla yazışma" (bütün ustalar görür)
            var otherIsAdmin = (await AdminNamesAsync()).Contains(other);
            if (meIsAdmin && !otherIsAdmin) return (other, null);
            if (!meIsAdmin && otherIsAdmin) return (me, null);

            // Müştəri ↔ müştəri və usta ↔ usta: şəxsi yazışma
            var pair = new[] { me.ToLowerInvariant(), other.ToLowerInvariant() }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var directKey = pair[0] + "|" + pair[1];
            var group = await _context.ChatGroups.FirstOrDefaultAsync(g => g.IsDirect && g.DirectKey == directKey);
            if (group == null)
            {
                var now = AzTime.Now;
                group = new ChatGroup
                {
                    Name = L.T("Şəxsi yazışma"),
                    IsDirect = true,
                    DirectKey = directKey,
                    CreatedBy = me,
                    CreatedDate = now,
                    LastMessageDate = now
                };
                _context.ChatGroups.Add(group);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Eyni anda iki tərəf yaratdısa — mövcudu götür
                    _context.ChangeTracker.Clear();
                    group = await _context.ChatGroups.FirstAsync(g => g.IsDirect && g.DirectKey == directKey);
                }
            }

            // Hər iki üzv (əvvəl biri çıxarılıbsa belə) yenidən açıq üzvdür
            foreach (var u in new[] { me, other })
                await GetOrCreateMemberAsync(group.Id, u, explicitMember: true);
            await _context.SaveChangesAsync();
            return (group.Key, null);
        }

        // ================= YAYIM =================

        public async Task<(List<ChatMessageDto> Messages, string? Error)> BroadcastAsync(ChatBroadcastVM vm, string admin)
        {
            var text = Normalize(vm.Message);
            if (text == null && vm.File == null) return (new(), L.T("Mesaj və ya fayl əlavə edin."));
            if (text != null && text.Length > MaxMessageLength)
                return (new(), L.T("Mesaj maksimum {0} simvol ola bilər.", MaxMessageLength));

            var admins = await AdminNamesAsync();
            var recipients = (await _context.Users.AsNoTracking().Where(u => u.EmailConfirmed).Select(u => u.UserName!).ToListAsync())
                .Where(u => !admins.Contains(u)).ToList();
            if (recipients.Count == 0) return (new(), L.T("Mesaj göndəriləcək istifadəçi yoxdur."));

            // Fayl bir dəfə saxlanılır — bütün mesajlar eyni faylı göstərir
            string? url = null, type = null;
            if (vm.File != null)
            {
                var saved = await SaveFileAsync(vm.File);
                if (saved.Error != null) return (new(), saved.Error);
                (url, type) = (saved.Url, saved.Type);
            }

            var now = AzTime.Now;
            var created = new List<ChatMessage>();
            foreach (var user in recipients)
            {
                var msg = new ChatMessage
                {
                    Message = text,
                    UserName = admin,
                    ConversationUserName = user,
                    SenderIsAdmin = true,
                    AttachmentUrl = url,
                    AttachmentType = type,
                    CreatedDate = now
                };
                _context.ChatMessages.Add(msg);
                created.Add(msg);
            }
            await _context.SaveChangesAsync();

            var convs = await _context.ChatConversations.Where(c => recipients.Contains(c.UserName)).ToDictionaryAsync(c => c.UserName, StringComparer.OrdinalIgnoreCase);
            foreach (var msg in created)
            {
                if (!convs.TryGetValue(msg.ConversationUserName!, out var conv))
                {
                    conv = new ChatConversation { UserName = msg.ConversationUserName! };
                    _context.ChatConversations.Add(conv);
                }
                conv.LastMessageId = msg.Id;
                conv.LastMessageDate = now;
                conv.AdminReadId = Math.Max(conv.AdminReadId, msg.Id);
                conv.AdminDeliveredId = Math.Max(conv.AdminDeliveredId, msg.Id);
            }
            await _context.SaveChangesAsync();

            return (await MapAsync(created), null);
        }

        // ================= KÖMƏKÇİLƏR =================

        /// <summary>Mesajları DTO-ya çevirir: cavab verilən mesaj və reaksiyalar bir sorğu ilə yüklənir.</summary>
        private async Task<List<ChatMessageDto>> MapAsync(List<ChatMessage> items)
        {
            if (items.Count == 0) return new();

            var ids = items.Select(m => m.Id).ToList();
            var replyIds = items.Where(m => m.ReplyToId.HasValue).Select(m => m.ReplyToId!.Value).Distinct().ToList();

            var replies = replyIds.Count == 0
                ? new Dictionary<int, ChatMessage>()
                : await _context.ChatMessages.AsNoTracking()
                    .Where(m => replyIds.Contains(m.Id) && !m.SoftDeleted)
                    .ToDictionaryAsync(m => m.Id);

            var reactions = ids.Count > 500 ? new Dictionary<int, List<ChatReactionDto>>() : (await _context.ChatReactions.AsNoTracking()
                    .Where(r => ids.Contains(r.MessageId))
                    .OrderBy(r => r.Id)
                    .ToListAsync())
                .GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => g
                    .GroupBy(r => r.Emoji)
                    .Select(e => new ChatReactionDto { Emoji = e.Key, Users = e.Select(r => r.UserName).ToList() })
                    .ToList());

            return items.Select(m =>
            {
                ChatReplyDto? reply = null;
                if (m.ReplyToId is int rid)
                {
                    reply = replies.TryGetValue(rid, out var r)
                        ? new ChatReplyDto
                        {
                            Id = r.Id,
                            UserName = r.UserName,
                            IsAdmin = r.SenderIsAdmin,
                            Message = r.Message == null ? null
                                : r.Message.Length > ReplySnippetLength ? r.Message[..ReplySnippetLength] + "…" : r.Message,
                            AttachmentType = r.AttachmentType
                        }
                        : new ChatReplyDto { Id = rid, Deleted = true };
                }

                return new ChatMessageDto
                {
                    Id = m.Id,
                    Conversation = m.ConversationUserName ?? "",
                    UserName = m.UserName,
                    Message = m.Message,
                    IsAdmin = m.SenderIsAdmin,
                    AttachmentUrl = m.AttachmentUrl,
                    AttachmentType = m.AttachmentType,
                    IsEdited = m.IsEdited,
                    Date = m.CreatedDate.ToString("yyyy-MM-dd"),
                    Time = m.CreatedDate.ToString("HH:mm"),
                    ReplyTo = reply,
                    Reactions = reactions.GetValueOrDefault(m.Id) ?? new()
                };
            }).ToList();
        }

        /// <summary>Siyahı və bildiriş üçün qısa mətn.</summary>
        public static string Preview(ChatMessage m)
        {
            if (!string.IsNullOrWhiteSpace(m.Message))
                return m.Message.Length > 120 ? m.Message[..120] + "…" : m.Message;
            return m.AttachmentType switch
            {
                "audio" => "🎤 " + L.T("Səs mesajı"),
                "video" => "🎬 " + L.T("Video"),
                "image" => "📷 " + L.T("Şəkil"),
                _ => ""
            };
        }

        private static string? AvatarUrl(string? profileImage) =>
            string.IsNullOrEmpty(profileImage) ? null : "/uploads/avatars/" + profileImage;

        private static string? Iso(DateTime? d) => d?.ToString("yyyy-MM-ddTHH:mm:ss");

        private static string? Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Sətir sonlarını vahid formaya sal, kənar boşluqları sil
            return text.Replace("\r\n", "\n").Trim();
        }

        private async Task<(string? Url, string? Type, string? Error)> SaveFileAsync(IFormFile file)
        {
            var kind = AllowedUploadAttribute.DetectKind(file);
            if (kind == null)
                return (null, null, L.T("Yalnız şəkil (jpg, png, webp), video (mp4, webm, mov) və ya səs mesajı göndərilə bilər."));

            var max = kind switch
            {
                UploadKind.Image => MaxImageBytes,
                UploadKind.Audio => MaxAudioBytes,
                _ => MaxVideoBytes
            };
            if (!AllowedUploadAttribute.IsAllowed(file, kind.Value, out var error, max))
                return (null, null, error);

            var folder = Path.Combine(_env.WebRootPath, "uploads", "chat");
            Directory.CreateDirectory(folder);

            // Fayl adı istifadəçidən götürülmür — yalnız təsadüfi GUID + yoxlanmış uzantı
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            var type = kind switch
            {
                UploadKind.Image => "image",
                UploadKind.Audio => "audio",
                _ => "video"
            };
            return ($"/{UploadFolder}/{fileName}", type, null);
        }

        /// <summary>Fayl başqa mesajda (məs. yayım) istifadə olunmursa silinir.</summary>
        private async Task DeleteFileIfUnusedAsync(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (await _context.ChatMessages.AnyAsync(m => m.AttachmentUrl == url)) return;
            DeleteFile(url);
        }

        private void DeleteFile(string? url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith($"/{UploadFolder}/"))
                return;

            try
            {
                var fileName = Path.GetFileName(url);
                var fullPath = Path.Combine(_env.WebRootPath, "uploads", "chat", fileName);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Çat faylı silinə bilmədi: {Url}", url);
            }
        }
    }
}
