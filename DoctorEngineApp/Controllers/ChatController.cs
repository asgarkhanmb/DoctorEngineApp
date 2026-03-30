using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _service;

        public ChatController(IChatService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index()
        {
            var messages = await _service.GetAllAsync();
            return View(messages);
        }
    }
}
