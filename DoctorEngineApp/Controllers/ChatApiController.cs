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

        private readonly IChatService _service;
        private readonly IHubContext<ChatHub> _hub;

        public ChatApiController(IChatService service, IHubContext<ChatHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        private string? CurrentUserName => User.Identity?.Name;
        private bool IsAdmin => User.IsInRole(nameof(Roles.Admin));

        // Çat hamıya görünür; yazmaq üçün giriş lazımdır
        [AllowAnonymous]
        [HttpGet("messages")]
        public async Task<ActionResult<ChatPageDto>> Messages([FromQuery] int? beforeId, [FromQuery] int take = 30)
        {
            return await _service.GetPageAsync(beforeId, take);
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
            return Ok(result.Message);
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
                return NotFound(new { error = "Mesaj tapılmadı." });
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
                return NotFound(new { error = "Mesaj tapılmadı." });

            await _hub.Clients.All.SendAsync("DeleteMessage", id);
            return Ok();
        }
    }
}
