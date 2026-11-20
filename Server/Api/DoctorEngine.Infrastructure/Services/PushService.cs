using System.Globalization;
using System.Net;
using System.Text.Json;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Localization;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;
using PushEntity = DoctorEngineApp.Models.PushSubscription;

namespace DoctorEngineApp.Services
{
    /// <summary>Web Push abunəlikləri və yeni çat mesajı bildirişlərinin göndərilməsi.</summary>
    public class PushService
    {
        private readonly AppDbContext _context;
        private readonly PushOptions _options;
        private readonly EmailSettings _email;
        private readonly ILogger<PushService> _logger;

        public PushService(AppDbContext context, IOptions<PushOptions> options,
                           IOptions<EmailSettings> email, ILogger<PushService> logger)
        {
            _context = context;
            _options = options.Value;
            _email = email.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;
        public string? PublicKey => _options.IsConfigured ? _options.PublicKey : null;

        public async Task SubscribeAsync(string username, string endpoint, string p256dh, string auth)
        {
            // Eyni brauzer (endpoint) başqa hesabla daxil olubsa — yeni sahibə keçir
            var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
            if (existing != null)
            {
                existing.UserName = username;
                existing.P256dh = p256dh;
                existing.Auth = auth;
                existing.Lang = L.Lang;   // istifadəçi saytın dilini dəyişibsə, bildirişlər də o dildə gəlsin
            }
            else
            {
                await _context.PushSubscriptions.AddAsync(new PushEntity
                {
                    UserName = username,
                    Endpoint = endpoint,
                    P256dh = p256dh,
                    Auth = auth,
                    Lang = L.Lang,
                    CreatedDate = AzTime.Now
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task UnsubscribeAsync(string username, string endpoint)
        {
            var sub = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserName == username);
            if (sub == null) return;
            _context.PushSubscriptions.Remove(sub);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Yeni mesaj bildirişi — yalnız yazışmanın qarşı tərəfinə (şəxsi çat):
        /// müştəri yazıbsa ustalara, usta yazıbsa həmin müştəriyə.
        /// </summary>
        public async Task NotifyNewMessageAsync(ChatMessageDto msg)
        {
            if (!_options.IsConfigured) return;

            List<string> recipients;
            if (ChatService.GroupIdOf(msg.Conversation) is int gid)
            {
                // Qrup: üzvlər ("bütün istifadəçilər" qrupunda — hamı)
                var group = await _context.ChatGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gid);
                if (group == null) return;
                recipients = group.AllUsers
                    ? await _context.Users.Select(u => u.UserName!).ToListAsync()
                    : await _context.ChatGroupMembers.Where(m => m.GroupId == gid && m.IsExplicit).Select(m => m.UserName).ToListAsync();
            }
            else if (msg.IsAdmin)
            {
                recipients = new List<string> { msg.Conversation };
            }
            else
            {
                var adminRole = nameof(DoctorEngineApp.Helpers.Enums.Roles.Admin);
                recipients = await (
                    from u in _context.Users
                    join ur in _context.UserRoles on u.Id equals ur.UserId
                    join r in _context.Roles on ur.RoleId equals r.Id
                    where r.Name == adminRole
                    select u.UserName!
                ).ToListAsync();
            }

            var subs = await _context.PushSubscriptions
                .Where(s => s.UserName != msg.UserName && recipients.Contains(s.UserName))
                .ToListAsync();
            if (subs.Count == 0) return;

            // Hər abunəçiyə öz dilində (mesajın mətni olduğu kimi qalır — yalnız şablon sözlər tərcümə olunur)
            var payloads = new Dictionary<string, string>();
            string PayloadFor(string? lang)
            {
                lang = L.IsSupported(lang) ? lang! : L.Default;
                if (payloads.TryGetValue(lang, out var cached)) return cached;

                var previous = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lang);
                try
                {
                    var body = msg.Message;
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        body = msg.AttachmentType switch
                        {
                            "audio" => "🎤 " + L.T("Səs mesajı"),
                            "video" => "🎬 " + L.T("Video"),
                            "image" => "📷 " + L.T("Şəkil"),
                            _ => L.T("Yeni mesaj")
                        };
                    }
                    else if (body.Length > 140)
                    {
                        body = body[..140] + "…";
                    }

                    return payloads[lang] = JsonSerializer.Serialize(new
                    {
                        title = msg.IsAdmin ? $"{msg.UserName} · {L.T("Usta")}" : msg.UserName,
                        conversation = msg.Conversation,
                        body,
                        tag = "dc-chat",
                        // Usta üçün birbaşa həmin müştərinin yazışması açılır
                        url = msg.IsAdmin && !ChatService.IsGroupKey(msg.Conversation) ? "/?openChat=1" : "/Chat?c=" + Uri.EscapeDataString(msg.Conversation),
                        id = msg.Id
                    });
                }
                finally
                {
                    CultureInfo.CurrentUICulture = previous;
                }
            }

            var vapid = new VapidDetails(Subject(), _options.PublicKey, _options.PrivateKey);
            var client = new WebPushClient();
            var expired = new List<PushEntity>();

            foreach (var s in subs)
            {
                try
                {
                    await client.SendNotificationAsync(
                        new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth), PayloadFor(s.Lang), vapid);
                }
                catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                {
                    expired.Add(s);   // brauzer abunəliyi ləğv edib — sil
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Push bildirişi göndərilmədi ({User})", s.UserName);
                }
            }

            if (expired.Count > 0)
            {
                _context.PushSubscriptions.RemoveRange(expired);
                await _context.SaveChangesAsync();
            }
        }

        private string Subject()
        {
            if (!string.IsNullOrWhiteSpace(_options.Subject)) return _options.Subject!;
            if (!string.IsNullOrWhiteSpace(_email.From)) return "mailto:" + _email.From;
            return "https://localhost";
        }
    }
}
