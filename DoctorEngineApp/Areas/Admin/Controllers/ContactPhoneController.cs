using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.ContactPhones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YourProject.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ContactPhoneController : Controller
    {
        private readonly IContactPhoneService _service;

        public ContactPhoneController(IContactPhoneService service)
        {
            _service = service;
        }

        // ✅ LIST
        public async Task<IActionResult> Index()
        {
            var phones = await _service.GetAllAsync();
            return View(phones);
        }

        // ✅ CREATE (GET)
        public IActionResult Create()
        {
            return View();
        }

        // ✅ CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactPhoneVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            await _service.CreateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        // ✅ EDIT (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _service.GetByIdAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // ✅ EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ContactPhoneVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            await _service.UpdateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        // ✅ DELETE (GET)
        public async Task<IActionResult> Delete(int id)
        {
            var model = await _service.GetByIdAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // ✅ DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
