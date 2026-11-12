using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SiteSettingController : Controller
    {
        private readonly ISiteSettingService _service;

        public SiteSettingController(ISiteSettingService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var s = await _service.GetAsync();
            return View(new SiteSettingVM
            {
                SiteName = s.SiteName,
                LogoIcon = s.LogoIcon,
                ExistingLogo = s.LogoImage
            });
        }

        [HttpPost]
        public async Task<IActionResult> Index(SiteSettingVM vm)
        {
            if (!SiteDefaults.IsAllowedIcon(vm.LogoIcon))
                ModelState.AddModelError(nameof(vm.LogoIcon), "Siyahıdan ikon seçin");

            if (!ModelState.IsValid)
            {
                vm.ExistingLogo = (await _service.GetAsync()).LogoImage;
                return View(vm);
            }

            await _service.SaveAsync(vm);
            TempData["Success"] = "Sayt ayarları yadda saxlanıldı. Dəyişiklik bütün səhifələrdə dərhal görünür.";
            return RedirectToAction(nameof(Index));
        }
    }
}
