using System.Diagnostics;
using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
