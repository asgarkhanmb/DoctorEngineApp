using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.ViewComponents
{
    using DoctorEngineApp.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    public class SliderViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;

        public SliderViewComponent(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var sliders = await _context.Sliders.ToListAsync();

            return View(sliders);  // ← Model NULL olmamalıdır
        }
    }

}
