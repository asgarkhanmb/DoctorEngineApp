using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.ViewComponents
{
    public class SliderViewComponent: ViewComponent
    {

        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View();
        }
    }
    public class SliderVMVC
    {
        public IEnumerable<Slider> Sliders { get; set; }

    }
}
