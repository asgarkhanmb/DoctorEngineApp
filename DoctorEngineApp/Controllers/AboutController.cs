using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Infroms;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class AboutController : Controller
    {
        private readonly IAboutService _aboutService;
        private readonly IInformService _informService;
        private readonly ITechnicianService _technicianService;
        private readonly IContactInfoService _contactInfoService;

        public AboutController(
            IAboutService aboutService,
            IInformService informService,
            ITechnicianService technicianService,
            IContactInfoService contactInfoService)
        {
            _aboutService = aboutService;
            _informService = informService;
            _technicianService = technicianService;
            _contactInfoService = contactInfoService;
        }
        public async Task<IActionResult> Index()
        {
            var abouts = await _aboutService.GetAllAsync();
            var informs = await _informService.GetAllAsync();
            var technicians = await _technicianService.GetAllAsync();

            var about = abouts.FirstOrDefault();

            AboutVM model = new AboutVM
            {
                Title = about?.Title,
                Description = about?.Description,
                VideoPath = about?.VideoPath,

                Informs = informs.Select(i => new InformVM
                {
                    Title = i.Title,
                    Description = i.Description,
                    Icon = i.Icon
                }).ToList(),
                Technicians = technicians,
                Contact = await _contactInfoService.GetFirstAsync()
            };

            return View(model);
        }
    }
}
