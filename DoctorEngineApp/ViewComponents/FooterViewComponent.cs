using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.ViewComponents
{
    public class FooterViewComponent : ViewComponent
    {
        private readonly IContactInfoService _contactInfoService;
        private readonly IServiceService _serviceService;

        public FooterViewComponent(
            IContactInfoService contactInfoService,
            IServiceService serviceService)
        {
            _contactInfoService = contactInfoService;
            _serviceService = serviceService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
          
            var contactInfo = await _contactInfoService.GetFirstAsync();
            if (contactInfo == null)
                contactInfo = new();
            contactInfo.Services = (await _serviceService.GetAllAsync())?.ToList();

            return View(contactInfo);
        }
    }
}
