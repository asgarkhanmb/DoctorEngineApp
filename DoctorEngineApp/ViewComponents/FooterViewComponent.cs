using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.ViewComponents
{
    public class FooterViewComponent : ViewComponent
    {
        private readonly IServiceService _serviceService;

        public FooterViewComponent(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var services = await _serviceService.GetAllAsync();
            return View(services);
        }
    }
}
