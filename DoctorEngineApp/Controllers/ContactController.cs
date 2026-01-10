using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class ContactController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
