using DoctorEngineApp.Localization;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.Web;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngine.Api.Controllers
{
    /// <summary>
    /// Çat — mobil / desktop tətbiqlər üçün (Bearer). Bütün endpoint-lər ortaq bazadadır:
    /// Api/DoctorEngine.Infrastructure/Web/ChatControllerBase.cs (sayt da eynisini istifadə edir).
    /// Push abunəliyi saytdakı api/chat/push/* ilə eynidir (form sahələri: endpoint, p256dh, auth).
    /// </summary>
    [Route("api/chat")]
    [ApiController]
    public class ChatController : ChatControllerBase
    {
        // Push bildirişləri yalnız bu push serverlərinə göndərilir (SSRF qoruması) — saytdakı siyahı
        private static readonly string[] PushHosts =
        {
            "fcm.googleapis.com",            // Chrome, Edge (köhnə), Opera, Android
            "push.services.mozilla.com",     // Firefox
            "notify.windows.com",            // Edge (Windows)
            "push.apple.com"                 // Safari / iOS
        };

        public ChatController(IChatService service, IChatNotifier notifier, PushService push, IServiceScopeFactory scopeFactory)
            : base(service, notifier, push, scopeFactory) { }

        // ================= PUSH =================

        /// <summary>
        /// Çat vidjeti üçün ayarlar (saytın səhifəsi bunu göstərmədən əvvəl alır): limitlər, reaksiyalar,
        /// oxunmamış mesajlar, push açarı. Anonim istifadəçiyə də cavab verir (unreadCount = 0).
        /// </summary>
        [HttpGet("config")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> Config()
        {
            var signedIn = User.Identity?.IsAuthenticated == true;
            return Ok(new
            {
                maxLength = ChatService.MaxMessageLength,
                maxImageBytes = ChatService.MaxImageBytes,
                maxVideoBytes = ChatService.MaxVideoBytes,
                maxAudioBytes = ChatService.MaxAudioBytes,
                reactions = ChatService.AllowedReactions,
                unreadCount = signedIn ? await Service.GetUnreadCountAsync(Me, IsAdmin) : 0,
                pushKey = signedIn && Push.IsConfigured ? Push.PublicKey : null
            });
        }

        /// <summary>Brauzer push üçün VAPID açarı (bildirişlər qurulmayıbsa null).</summary>
        [HttpGet("push/key")]
        public IActionResult PushKey() => Ok(new { key = Push.IsConfigured ? Push.PublicKey : null });

        [HttpPost("push/subscribe")]
        public async Task<IActionResult> PushSubscribe([FromForm] string endpoint, [FromForm] string p256dh, [FromForm] string auth)
        {
            if (!Push.IsConfigured)
                return BadRequest(new { error = L.T("Bildirişlər serverdə qurulmayıb.") });

            if (!IsAllowedPushEndpoint(endpoint) ||
                string.IsNullOrWhiteSpace(p256dh) || p256dh.Length > 200 ||
                string.IsNullOrWhiteSpace(auth) || auth.Length > 100)
                return BadRequest(new { error = L.T("Abunəlik məlumatı düzgün deyil.") });

            await Push.SubscribeAsync(Me, endpoint, p256dh, auth);
            return Ok();
        }

        [HttpPost("push/unsubscribe")]
        public async Task<IActionResult> PushUnsubscribe([FromForm] string endpoint)
        {
            if (!string.IsNullOrEmpty(endpoint))
                await Push.UnsubscribeAsync(Me, endpoint);
            return Ok();
        }

        private static bool IsAllowedPushEndpoint(string? endpoint)
        {
            if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 800)
                return false;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;
            var host = uri.Host.ToLowerInvariant();
            return PushHosts.Any(h => host == h || host.EndsWith("." + h));
        }
    }
}
