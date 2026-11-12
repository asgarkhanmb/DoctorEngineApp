using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.ContactPhones;
using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Reservations;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Controllers
{
    public class ReservationController : Controller
    {
        private readonly IInformService _informService;
        private readonly IReservationService _reservationService;
        private readonly IContactPhoneService _contactPhoneService;
        private readonly AppDbContext _db;
        private readonly ILogger<ReservationController> _logger;

        public ReservationController(
            IInformService informService,
            IReservationService reservationService,
            IContactPhoneService contactPhoneService,
            AppDbContext db,
            ILogger<ReservationController> logger)
        {
            _informService = informService;
            _reservationService = reservationService;
            _contactPhoneService = contactPhoneService;
            _db = db;
            _logger = logger;
        }
        public async Task<IActionResult> Index()
        {
            var informs = await _informService.GetAllAsync();

            var services = await _db.Services
                .Select(s => new ServiceVM
                {
                    Id = s.Id,
                    Name = s.Name
                })
                .ToListAsync();

            // LIST alırıq
            var contactPhones = await _contactPhoneService.GetAllAsync();

            // BİR dənə götürürük (yoxdursa null)
            var contactPhone = contactPhones.FirstOrDefault();

            var vm = new ReservationPageVM
            {
                Informs = informs?.Select(x => new InformVM
                {
                    Icon = x.Icon,
                    Title = x.Title,
                    Description = x.Description
                }).ToList() ?? new List<InformVM>(),

                Reservation = new ReservationCreateVM
                {
                    Services = services,
                    DateTime = AzTime.Now.AddHours(1)
                },

                ContactPhone = contactPhone ?? new ContactPhoneVM()
            };

            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(
            [Bind(Prefix = "Reservation")] ReservationCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

                return Json(new { success = false, message = firstError ?? "Məlumatları düzgün doldurun!" });
            }

            try
            {
                var result = await _reservationService.TryCreateAsync(model);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rezervasiya yaradılarkən xəta");
                return Json(new { success = false, message = "Xəta baş verdi. Yenidən cəhd edin." });
            }
        }
    }
}
