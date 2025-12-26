using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace DoctorEngineApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IInformService _informService;
        private readonly IAboutService _aboutService;
        private readonly AppDbContext _db;

        public HomeController(
            IInformService informService,
            IAboutService aboutService,
            AppDbContext db)
        {
            _informService = informService;
            _aboutService = aboutService;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            // ================= INFORM =================
            var informs = await _informService.GetAllAsync();
            var informVMs = informs.Select(x => new InformVM
            {
                Icon = x.Icon,
                Title = x.Title,
                Description = x.Description
            }).ToList();

            // ================= ABOUT =================
            var about = (await _aboutService.GetAllAsync()).FirstOrDefault();
            var aboutVMs = about != null
                ? new List<AboutVM>
                {
                    new AboutVM
                    {
                        Title = about.Title,
                        Description = about.Description,
                        VideoPath = about.VideoPath
                    }
                }
                : new List<AboutVM>();

            // ================= CATEGORIES + SUBSERVICES =================
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
                })
                .ToListAsync();

            // ================= HOME VM =================
            var vm = new HomeVM
            {
                Informs = informVMs,
                Abouts = aboutVMs,
                Categories = categories
            };

            return View(vm);
        }
    }
}
