using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ContactMessageController : Controller
    {
        private readonly IContactMessageService _contactMessageService;

        public ContactMessageController(IContactMessageService contactMessageService)
        {
            _contactMessageService = contactMessageService;
        }

        public async Task<IActionResult> Index()
        {
            var messages = await _contactMessageService.GetAllAsync();
            return View(messages);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var message = await _contactMessageService.GetByIdAsync(id);

            if (message == null)
                return NotFound();

            return View(message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _contactMessageService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
