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
                Technicians = technicians
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(
         [Bind(Prefix = "Reservation")] ReservationCreateVM model)
        {
            bool alreadyExists = await _reservationService.ExistsAsync(
                model.Phone,
                model.ServiceId,
                model.DateTime
            );

            if (alreadyExists)
            {
                return Json(new
                {
                    success = false,
                    message = "Bu rezervasiya artıq mövcuddur."
                });
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Bütün sahələri düzgün doldurun!"
                });
            }

            // Keçmiş tarix
            if (model.DateTime < DateTime.Now)
            {
                return Json(new
                {
                    success = false,
                    message = "Keçmiş tarix üçün rezervasiya edilə bilməz."
                });
            }

            // İş saatları: 10:00 – 20:00
            if (model.DateTime.Hour < 10 || model.DateTime.Hour >= 20)
            {
                return Json(new
                {
                    success = false,
                    message = "Rezervasiya yalnız 10:00 – 20:00 arası mümkündür."
                });
            }

            // Maksimum 10 gün qabaqcadan
            if (model.DateTime > DateTime.Now.AddDays(10))
            {
                return Json(new
                {
                    success = false,
                    message = "Rezervasiya maksimum 10 gün öncədən edilə bilər."
                });
            }

            try
            {
                // 🇦🇿 Azərbaycan vaxtına çevirmə
                model.DateTime = TimeZoneInfo.ConvertTime(
                    model.DateTime,
                    TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time")
                );

                await _reservationService.CreateAsync(model);

                return Json(new
                {
                    success = true,
                    message = "Rezervasiya qəbul edildi. WhatsApp üzərindən əlaqə saxlanılacaq."
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Xəta baş verdi. Yenidən cəhd edin."
                });
            }
        }



    }
}
