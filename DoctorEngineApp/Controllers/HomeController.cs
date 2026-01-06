using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Reservations;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace DoctorEngineApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IInformService _informService;
        private readonly IAboutService _aboutService;
        private readonly IReservationService _reservationService;
        private readonly AppDbContext _db;

        public HomeController(
            IInformService informService,
            IAboutService aboutService,
            IReservationService reservationService,
            AppDbContext db)
        {
            _informService = informService;
            _aboutService = aboutService;
            _reservationService = reservationService;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var informs = await _informService.GetAllAsync();
            var about = (await _aboutService.GetAllAsync()).FirstOrDefault();

            var categories = await _db.ServiceCategories
                .Include(c => c.SubServices)
                .Select(c => new CategoryVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    Icon = c.Icon,
                    SubServices = c.SubServices.Select(s => new SubServiceVM
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Description = s.Description,
                        Video = s.Video
                    }).ToList()
                }).ToListAsync();   

            var services = await _db.Services
                .Select(s => new ServiceVM
                {
                    Id = s.Id,
                    Name = s.Name
                }).ToListAsync();

            // 👇 TEXNİKLƏR
            var technicians = await _db.Technicians
                .Where(t => !t.IsDeleted)
                .ToListAsync();

            HomeVM vm = new HomeVM
            {
                Informs = informs.Select(x => new InformVM
                {
                    Icon = x.Icon,
                    Title = x.Title,
                    Description = x.Description
                }).ToList(),

                Abouts = about != null
                    ? new() {
                new AboutVM
                {
                    Title = about.Title,
                    Description = about.Description,
                    VideoPath = about.VideoPath
                }
                    }
                    : new(),

                Categories = categories,

                Reservation = new ReservationCreateVM
                {
                    Services = services
                },

                // 👇 BURASI
                Technicians = technicians
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(
       [Bind(Prefix = "Reservation")] ReservationCreateVM model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Bütün sahələri doldurun!" });

            try
            {
                // AZ vaxtı
                var azTime = TimeZoneInfo.ConvertTime(
                    model.DateTime,
                    TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time")
                );
                model.DateTime = azTime;

                await _reservationService.CreateAsync(model);

                return Json(new
                {
                    success = true,
                    message = "Rezervasiya qəbul edildi. WhatsApp üzərindən əlaqə saxlanılacaq."
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch
            {
                return Json(new { success = false, message = "Xəta baş verdi. Yenidən cəhd edin." });
            }
        }


    }
}
