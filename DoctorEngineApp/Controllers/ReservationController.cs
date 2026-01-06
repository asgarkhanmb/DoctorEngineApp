using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Reservations;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class ReservationController : Controller
    {
        private readonly IReservationService _reservationService;
        private readonly AppDbContext _context;

        public ReservationController(IReservationService reservationService, AppDbContext context)
        {
            _reservationService = reservationService;
            _context = context;
        }
        public IActionResult Success()
        {
            return View();
        }
    }
}
