using DoctorEngineApp.Data;
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

        public ReservationController(
            IInformService informService,
            IReservationService reservationService,
            IContactPhoneService contactPhoneService,
            AppDbContext db)
        {
            _informService = informService;
            _reservationService = reservationService;
            _contactPhoneService = contactPhoneService;
            _db = db;
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
                    DateTime = DateTime.Now.AddHours(1)
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
                return Json(new { success = false, message = "Məlumatları düzgün doldurun!" });
            }

            if (model.DateTime < DateTime.Now)
            {
                return Json(new { success = false, message = "Keçmiş tarix olmaz!" });
            }

            if (model.DateTime.Hour < 10 || model.DateTime.Hour >= 20)
            {
                return Json(new { success = false, message = "Saat 10:00 – 20:00 arası mümkündür." });
            }

            if (model.DateTime > DateTime.Now.AddDays(10))
            {
                return Json(new { success = false, message = "Maksimum 10 gün öncədən olar." });
            }

            bool exists = await _reservationService.ExistsAsync(
                model.Phone,
                model.ServiceId,
                model.DateTime
            );

            if (exists)
            {
                return Json(new { success = false, message = "Bu rezervasiya artıq var." });
            }

            await _reservationService.CreateAsync(model);

            return Json(new
            {
                success = true,
                message = "Rezervasiya qəbul edildi. WhatsApp ilə əlaqə saxlanılacaq."
            });
        }
    }
}
