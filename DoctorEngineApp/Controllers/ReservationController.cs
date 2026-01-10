using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Reservations;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class ReservationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
