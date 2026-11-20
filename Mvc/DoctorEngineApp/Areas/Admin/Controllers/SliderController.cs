using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Sliders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SliderController : Controller
    {
        private readonly ISliderService _service;

        public SliderController(ISliderService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _service.GetAllAsync());
        }

        public IActionResult Create()
        {
            return View(new SliderVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(SliderVM vm)
        {
            if (vm.ImageFile1 == null)
                ModelState.AddModelError(nameof(vm.ImageFile1), "Fon şəkli məcburidir");

            if (!ModelState.IsValid) return View(vm);

            await _service.CreateAsync(vm);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var slider = await _service.GetByIdAsync(id);
            if (slider == null) return NotFound();

            SliderVM vm = new SliderVM
            {
                Id = slider.Id,
                Title = slider.Title,
                Subtitle = slider.Subtitle,
                ExistingImage1 = slider.Image1,
                ExistingImage2 = slider.Image2
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SliderVM vm)
        {
            if (!ModelState.IsValid)
            {
                // Forma yenidən göstəriləndə mövcud şəkillər itməsin
                var current = await _service.GetByIdAsync(vm.Id);
                if (current == null) return NotFound();
                vm.ExistingImage1 = current.Image1;
                vm.ExistingImage2 = current.Image2;
                return View(vm);
            }

            await _service.UpdateAsync(vm);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
