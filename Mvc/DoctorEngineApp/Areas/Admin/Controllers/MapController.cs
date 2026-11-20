using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Maps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MapController : Controller
    {
        private readonly IMapService _service;

        public MapController(IMapService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index()
        {
            if (!await _service.ExistsAsync())
                return RedirectToAction(nameof(Create));

            return RedirectToAction(nameof(Edit));
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (await _service.ExistsAsync())
                return RedirectToAction(nameof(Edit));

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(MapVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            await _service.CreateAsync(model);
            return RedirectToAction(nameof(Edit));
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            return View(await _service.GetSingleAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MapVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            await _service.UpdateAsync(model);
            return RedirectToAction(nameof(Edit));
        }
    }
}
