using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ServiceController : Controller
    {
        private readonly IServiceService _serviceService;

        public ServiceController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        public async Task<IActionResult> Index()
        {
            var services = await _serviceService.GetAllAsync();
            return View(services);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ServiceVM model)
        {
            if (!ModelState.IsValid) return View(model);

            await _serviceService.CreateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var service = await _serviceService.GetByIdAsync(id);
            if (service == null) return NotFound();
            return View(service);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ServiceVM model)
        {
            if (!ModelState.IsValid) return View(model);

            await _serviceService.UpdateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            await _serviceService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }

}
