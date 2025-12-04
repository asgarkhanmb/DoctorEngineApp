using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels;
using DoctorEngineApp.ViewModels.Infroms;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IInformService _informService;

        public HomeController(IInformService informService)
        {
            _informService = informService;
        }

        public async Task<IActionResult> Index()
        {
            var informs = await _informService.GetAllAsync();

            var vm = new HomeVM
            {
                Informs = informs.Select(x => new InformVM
                {
                    Icon = x.Icon, 
                    Title = x.Title,
                    Description = x.Description
                }).ToList()
            };

            return View(vm);
        }
    }
}
