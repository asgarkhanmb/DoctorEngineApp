using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Infroms;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IInformService _informService;
        private readonly IAboutService _aboutService;

        public HomeController(IInformService informService, IAboutService aboutService)
        {
            _informService = informService;
            _aboutService = aboutService;
        }

        public async Task<IActionResult> Index()
        {
            var informs = await _informService.GetAllAsync();
            var abouts = await _aboutService.GetAllAsync(); // List qaytarır

            var about = abouts.FirstOrDefault(); // İlk element

            var vm = new HomeVM
            {
                Informs = informs.Select(x => new InformVM
                {
                    Icon = x.Icon,
                    Title = x.Title,
                    Description = x.Description
                }).ToList(),

                Abouts = new List<AboutVM>()
            };

            if (about != null)
            {
                vm.Abouts.Add(new AboutVM
                {
                    Title = about.Title,
                    Description = about.Description,
                    VideoPath = about.VideoPath   // <----- Səhv buradaydı
                });
            }

            return View(vm);
        }


    }
}
