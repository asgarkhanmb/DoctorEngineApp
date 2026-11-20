using DoctorEngineApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    /// <summary>AI köməkçi səhifəsi (/Assistant)</summary>
    public class AssistantController : Controller
    {
        private readonly AiAssistantService _ai;

        public AssistantController(AiAssistantService ai)
        {
            _ai = ai;
        }

        public IActionResult Index()
        {
            ViewBag.AiConfigured = _ai.IsConfigured;
            ViewBag.AiQuota = _ai.GetQuota(HttpContext);   // bu gün qalan suallar
            return View();
        }
    }
}
