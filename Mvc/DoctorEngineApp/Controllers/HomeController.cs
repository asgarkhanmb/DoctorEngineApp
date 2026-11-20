using DoctorEngineApp.Localization;
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
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IInformService informService,
            IAboutService aboutService,
            IReservationService reservationService,
            AppDbContext db,
            ILogger<HomeController> logger)
        {
            _informService = informService;
            _aboutService = aboutService;
            _reservationService = reservationService;
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var informs = await _informService.GetAllAsync();
            var about = (await _aboutService.GetAllAsync()).FirstOrDefault();

            var categories = await _db.ServiceCategories
                .Include(c => c.SubServices)
                .OrderBy(c => c.Id)
                .Select(c => new CategoryVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    Icon = c.Icon,
                    SubServices = c.SubServices.OrderBy(s => s.Id).Select(s => new SubServiceVM
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
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

                return Json(new
                {
                    success = false,
                    message = firstError ?? L.T("Bütün sahələri düzgün doldurun!")
                });
            }

            try
            {
                var result = await _reservationService.TryCreateAsync(model);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rezervasiya yaradılarkən xəta");
                return Json(new
                {
                    success = false,
                    message = L.T("Xəta baş verdi. Yenidən cəhd edin.")
                });
            }
        }



    }
}
