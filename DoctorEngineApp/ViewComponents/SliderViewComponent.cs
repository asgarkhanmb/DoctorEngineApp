using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.ViewComponents
{
    public class SliderViewComponent : ViewComponent
    {
        private readonly ISliderService _service;

        public SliderViewComponent(ISliderService service)
        {
            _service = service;
        }

        // Yalnız silinməmiş slaydlar, sabit sıra ilə
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View(await _service.GetAllAsync());
        }
    }
}
