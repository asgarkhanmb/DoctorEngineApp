using DoctorEngineApp.Localization;
using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Hubs;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DoctorEngineApp.Controllers
{
    [Route("api/chat")]
    [ApiController]
    [Authorize]
    public class ChatApiController : ControllerBase
    {
        // Maksimum video + form üçün kiçik ehtiyat
        private const long MaxRequestBytes = ChatService.MaxVideoBytes + 1024 * 1024;

        // Push bildirişləri yalnız bu brauzer push serverlərinə göndərilir (SSRF qoruması)
        private static readonly string[] PushHosts =
        {
            "fcm.googleapis.com",            // Chrome, Edge (köhnə), Opera, Android
            "push.services.mozilla.com",     // Firefox
            "notify.windows.com",            // Edge (Windows)
            "push.apple.com"                 // Safari / iOS
        };

        private readonly IChatService _service;
        private readonly IHubContext<ChatHub> _hub;
        private readonly PushService _push;
        private readonly IServiceScopeFactory _scopeFactory;

        public ChatApiController(IChatService service, IHubContext<ChatHub> hub,
                                 PushService push, IServiceScopeFactory scopeFactory)
        {
            _service = service;
            _hub = hub;
            _push = push;
            _scopeFactory = scopeFactory;
        }

        private string? CurrentUserName => User.Identity?.Name;
        private bool IsAdmin => User.IsInRole(nameof(Roles.Admin));

        // Yazışmaları yalnız daxil olmuş istifadəçilər görür
        [HttpGet("messages")]
        public async Task<ActionResult<ChatPageDto>> Messages([FromQuery] int? beforeId, [FromQuery] int take = 30)
        {
            return await _service.GetPageAsync(beforeId, take);
        }

        [HttpGet("unread")]
        public async Task<IActionResult> Unread()
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();
            return Ok(new { count = await _service.GetUnreadCountAsync(username) });
        }

        [HttpPost("read")]
        public async Task<IActionResult> Read([FromForm] int lastId)
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();
            if (lastId > 0)
                await _service.MarkReadAsync(username, lastId);
            return Ok();
        }

        [HttpPost("send")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Send([FromForm] ChatSendVM vm)
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var result = await _service.SendAsync(vm, username, IsAdmin);
            if (result.Status != ChatResultStatus.Ok)
                return BadRequest(new { error = result.Error });

            await _hub.Clients.All.SendAsync("ReceiveMessage", result.Message);
            QueuePush(result.Message!);
            return Ok(result.Message);
        }

        // ================= WEB PUSH =================

        [HttpPost("push/subscribe")]
        public async Task<IActionResult> PushSubscribe([FromForm] string endpoint, [FromForm] string p256dh, [FromForm] string auth)
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();
            if (!_push.IsConfigured)
                return BadRequest(new { error = L.T("Bildirişlər serverdə qurulmayıb.") });

            if (!IsAllowedPushEndpoint(endpoint) ||
                string.IsNullOrWhiteSpace(p256dh) || p256dh.Length > 200 ||
                string.IsNullOrWhiteSpace(auth) || auth.Length > 100)
                return BadRequest(new { error = L.T("Abunəlik məlumatı düzgün deyil.") });

            await _push.SubscribeAsync(username, endpoint, p256dh, auth);
            return Ok();
        }

        [HttpPost("push/unsubscribe")]
        public async Task<IActionResult> PushUnsubscribe([FromForm] string endpoint)
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();
            if (!string.IsNullOrEmpty(endpoint))
                await _push.UnsubscribeAsync(username, endpoint);
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

        /// <summary>Bildirişlər cavabı gecikdirməsin — arxa planda, ayrıca scope ilə göndərilir.</summary>
        private void QueuePush(ChatMessageDto message)
        {
            if (!_push.IsConfigured) return;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<PushService>().NotifyNewMessageAsync(message);
                }
                catch (Exception ex)
                {
                    scope.ServiceProvider.GetRequiredService<ILogger<ChatApiController>>()
                        .LogWarning(ex, "Push bildirişləri göndərilmədi");
                }
            });
        }

        [HttpPost("edit")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Edit([FromForm] ChatEditVM vm)
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            var result = await _service.EditAsync(vm, username);
            if (result.Status == ChatResultStatus.NotFound)
                return NotFound(new { error = L.T("Mesaj tapılmadı.") });
            if (result.Status == ChatResultStatus.Invalid)
                return BadRequest(new { error = result.Error });

            await _hub.Clients.All.SendAsync("EditMessage", result.Message);
            return Ok(result.Message);
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var username = CurrentUserName;
            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            if (!await _service.DeleteAsync(id, username, IsAdmin))
                return NotFound(new { error = L.T("Mesaj tapılmadı.") });

            await _hub.Clients.All.SendAsync("DeleteMessage", id);
            return Ok();
        }
    }
}
