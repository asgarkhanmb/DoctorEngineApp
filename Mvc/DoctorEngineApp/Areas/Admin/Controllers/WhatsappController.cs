using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WhatsAppController : Controller
    {
        private readonly IWhatsappService _service;

        public WhatsAppController(IWhatsappService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index()
        {
            var data = await _service.GetAsync();
            return View(data);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(WhatsappSetting model)
        {
            if (!ModelState.IsValid) return View(model);

            await _service.CreateAsync(model);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit()
        {
            var data = await _service.GetAsync();
            return View(data);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(WhatsappSetting model)
        {
            if (!ModelState.IsValid) return View(model);

            await _service.UpdateAsync(model);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var data = await _service.GetAsync();
            if (data == null || data.Id != id)
                return NotFound();

            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}