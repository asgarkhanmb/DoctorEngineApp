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
        private readonly IChatService _chatService; // ✅ ƏLAVƏ OLUNDU

        public ContactController(
            IContactInfoService contactInfoService,
            IMapService mapService,
            IContactMessageService contactMessageService,
            IChatService chatService) // ✅ BURDA DA ƏLAVƏ OLUNDU
        {
            _contactInfoService = contactInfoService;
            _mapService = mapService;
            _contactMessageService = contactMessageService;
            _chatService = chatService; // ✅ ASSIGN
        }

        // ================= GET =================
        public async Task<IActionResult> Index()
        {
            ContactPageVM model = new ContactPageVM
            {
                Contact = await _contactInfoService.GetFirstAsync(),
                Map = await _mapService.GetFirstAsync(),
                ChatMessages = await _chatService.GetAllAsync(), // ✅ CHAT
                Message = new ContactMessageCreateVM()
            };

            return View(model);
        }

        // ================= POST =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(ContactPageVM model)
        {
            if (!ModelState.IsValid)
            {
                model.Contact = await _contactInfoService.GetFirstAsync();
                model.Map = await _mapService.GetFirstAsync();
                model.ChatMessages = await _chatService.GetAllAsync(); // ⚠️ UNUTMA

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

            // ⚠️ HƏR HALDA YENİDƏN DOLDUR
            model.Contact = await _contactInfoService.GetFirstAsync();
            model.Map = await _mapService.GetFirstAsync();
            model.ChatMessages = await _chatService.GetAllAsync(); // ✅ VACİB

            return View("Index", model);
        }
    }
}