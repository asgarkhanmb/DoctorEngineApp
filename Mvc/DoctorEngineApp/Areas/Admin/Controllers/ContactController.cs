using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.ContactInfos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ContactController : Controller
    {
        private readonly IContactInfoService _service;

        public ContactController(IContactInfoService service)
        {
            _service = service;
        }

        // Dashboard / list (if needed)
        public IActionResult Index() => RedirectToAction(nameof(Edit));   // əvvəl səhv view (WhatsApp) göstərirdi → 500

        // GET: Create
        public async Task<IActionResult> Create()
        {
            if (await _service.ExistsAsync())
                return RedirectToAction("Edit"); // already exists

            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactInfoVm model)
        {
            if (!ModelState.IsValid) return View(model);

            await _service.CreateAsync(model);
            TempData["Success"] = "Əlaqə məlumatları yaradıldı.";
            return RedirectToAction("Edit");
        }

        // GET: Edit
        public async Task<IActionResult> Edit()
        {
            var model = await _service.GetFirstAsync();
            if (model == null) return RedirectToAction("Create");

            return View(model);
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ContactInfoVm model)
        {
            if (!ModelState.IsValid) return View(model);

            await _service.UpdateAsync(model);

            TempData["Success"] = "Əlaqə məlumatları yeniləndi.";
            return RedirectToAction("Edit");
        }

    }
}
