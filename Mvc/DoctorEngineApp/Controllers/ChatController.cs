using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        // Tam ekran çat səhifəsi (mesajlar brauzerdə /api/chat/messages ilə yüklənir)
        public IActionResult Index() => View();
    }
}
