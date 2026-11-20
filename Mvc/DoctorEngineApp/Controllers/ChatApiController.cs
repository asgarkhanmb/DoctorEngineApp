using DoctorEngineApp.Localization;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.Web;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    /// <summary>
    /// Çat — saytın brauzer klienti (cookie + antiforgery). Ümumi endpoint-lər (mesajlar, qruplar, yayım, ✓✓ ...)
    /// ortaq bazadadır: Api/DoctorEngine.Infrastructure/Web/ChatControllerBase.cs. Burada yalnız brauzer push abunəliyi.
    /// </summary>
    [Route("api/chat")]
    [ApiController]
    public class ChatApiController : ChatControllerBase
    {
        // Push bildirişləri yalnız bu brauzer push serverlərinə göndərilir (SSRF qoruması)
        private static readonly string[] PushHosts =
        {
            "fcm.googleapis.com",            // Chrome, Edge (köhnə), Opera, Android
            "push.services.mozilla.com",     // Firefox
            "notify.windows.com",            // Edge (Windows)
            "push.apple.com"                 // Safari / iOS
        };

        private readonly Microsoft.AspNetCore.Identity.SignInManager<DoctorEngineApp.Models.AppUser> _signIn;

        public ChatApiController(IChatService service, IChatNotifier notifier, PushService push, IServiceScopeFactory scopeFactory,
                                 Microsoft.AspNetCore.Identity.SignInManager<DoctorEngineApp.Models.AppUser> signIn)
            : base(service, notifier, push, scopeFactory)
        {
            _signIn = signIn;
        }

        /// <summary>Çatda profil dəyişdi — saytın başlığındakı ad / şəkil (cookie) yenilənsin.</summary>
        protected override async Task OnProfileChangedAsync()
        {
            var user = await _signIn.UserManager.GetUserAsync(User);
            if (user != null) await _signIn.RefreshSignInAsync(user);
        }

        // ================= WEB PUSH =================

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
