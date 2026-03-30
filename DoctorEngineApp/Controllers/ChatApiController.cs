using DoctorEngineApp.Hubs;
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
        private readonly IChatService _service;
        private readonly IHubContext<ChatHub> _hub;

        public ChatApiController(IChatService service, IHubContext<ChatHub> hub)
        {
            _service = service;
            _hub = hub;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] ChatMessageVM vm)
        {
            var username = User.Identity.Name;

            var msg = await _service.AddAsync(vm.Message, username);

            await _hub.Clients.All.SendAsync("ReceiveMessage", msg.UserName, msg.Message, msg.Id);

            return Ok();
        }
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var username = User.Identity.Name;

            await _service.DeleteAsync(id, username);

            await _hub.Clients.All.SendAsync("DeleteMessage", id);

            return Ok();
        }
        [HttpPost("edit")]
        public async Task<IActionResult> Edit([FromBody] EditMessageVM vm)
        {
            var username = User.Identity.Name;

            var msg = await _service.UpdateAsync(vm.Id, vm.Message, username);

            await _hub.Clients.All.SendAsync("EditMessage", msg.Id, msg.Message);

            return Ok();
        }
    }
}