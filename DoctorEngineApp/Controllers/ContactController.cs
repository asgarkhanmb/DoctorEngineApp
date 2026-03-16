using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.ContactMessage;
using DoctorEngineApp.ViewModels.ContactPhones;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class ContactController : Controller
    {
        private readonly IContactInfoService _contactInfoService;
        private readonly IMapService _mapService;
        private readonly IContactMessageService _contactMessageService;

        public ContactController(
            IContactInfoService contactInfoService,
            IMapService mapService,
            IContactMessageService contactMessageService)
        {
            _contactInfoService = contactInfoService;
            _mapService = mapService;
            _contactMessageService = contactMessageService;
        }

        public async Task<IActionResult> Index()
        {
            return View(new ContactPageVM
            {
                Contact = await _contactInfoService.GetFirstAsync(),
                Map = await _mapService.GetFirstAsync(),
                Message = new ContactMessageCreateVM()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(ContactPageVM model)
        {
            if (!ModelState.IsValid)
            {
                model.Contact = await _contactInfoService.GetFirstAsync();
                model.Map = await _mapService.GetFirstAsync();
                return View("Index", model);
            }

            try
            {
                await _contactMessageService.CreateAsync(model.Message);

                TempData["SuccessMessage"] =
                    "Mesajınız uğurla göndərildi. Tezliklə sizinlə əlaqə saxlanılacaq.";

                ModelState.Clear();
                model.Message = new ContactMessageCreateVM();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            model.Contact = await _contactInfoService.GetFirstAsync();
            model.Map = await _mapService.GetFirstAsync();

            return View("Index", model);
        }




    }
}
