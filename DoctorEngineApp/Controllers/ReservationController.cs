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
        // serviceId: Xidmətlər səhifəsindən gələndə həmin xidmət formada əvvəlcədən seçilir
        public async Task<IActionResult> Index(int? serviceId = null)
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
                    DateTime = AzTime.Now.AddHours(1),
                    ServiceId = serviceId.HasValue && services.Any(s => s.Id == serviceId.Value) ? serviceId.Value : 0
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
                return Json(new { success = result.Success, message = result.Message, trackingCode = result.TrackingCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rezervasiya yaradılarkən xəta");
                return Json(new { success = false, message = "Xəta baş verdi. Yenidən cəhd edin." });
            }
        }

        // Xidmət və gün üçün saatlar: GET /Reservation/Slots?serviceId=5&date=2026-11-14
        [HttpGet]
        public async Task<IActionResult> Slots(int serviceId, DateTime date)
        {
            if (serviceId <= 0)
                return Json(Array.Empty<object>());

            var slots = await _reservationService.GetSlotsAsync(serviceId, date);
            return Json(slots.Select(s => new { t = s.Time, s = s.State }));
        }

        // Müştərinin öz rezervasiyaları (brauzerdə saxlanan izləmə kodları ilə)
        [HttpGet]
        public async Task<IActionResult> Lookup([FromQuery] string[] codes)
        {
            var items = await _reservationService.LookupAsync(codes ?? Array.Empty<string>());
            return Json(items);
        }

        // İzləmə səhifəsi: /Reservation/Status/{kod} — status canlı yenilənir
        [HttpGet]
        public async Task<IActionResult> Status(string id)
        {
            var item = (await _reservationService.LookupAsync(new[] { id ?? "" })).FirstOrDefault();
            if (item == null)
                return NotFound();

            return View(item);
        }
    }
}
