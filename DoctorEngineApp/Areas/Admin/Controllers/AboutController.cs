using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Abouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AboutController :Controller
    {
        private readonly IAboutService _service;

        public AboutController(IAboutService service)
        {
            _service = service;
        }

        // List
        public async Task<IActionResult> Index()
        {
            var list = await _service.GetAllAsync();
            return View(list);
        }

        // Details
        public async Task<IActionResult> Details(int id)
        {
            var about = await _service.GetByIdAsync(id);
            if (about == null) return NotFound();
            return View(about);
        }

        // Create GET
        public IActionResult Create()
        {
            return View();
        }

        // Create POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AboutCreateVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _service.CreateAsync(vm);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(vm);
            }
        }

        // Edit GET
        public async Task<IActionResult> Edit(int id)
        {
            var about = await _service.GetByIdAsync(id);
            if (about == null) return NotFound();

            var vm = new AboutEditVM
            {
                Id = about.Id,
                Title = about.Title,
                Description = about.Description,
                CurrentVideo = about.VideoPath
            };

            return View(vm);
        }

        // Edit POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AboutEditVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _service.UpdateAsync(vm);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(vm);
            }
        }

        // Delete GET (confirm)
        public async Task<IActionResult> Delete(int id)
        {
            var about = await _service.GetByIdAsync(id);
            if (about == null) return NotFound();
            return View(about);
        }

        // Delete POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // handle error and maybe redirect back with message
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
