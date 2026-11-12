using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Warranties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WarrantyController : Controller
    {
        private readonly IWarrantyService _service;

        public WarrantyController(IWarrantyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> Index()
        {
            var warranties = await _service.GetAllAsync();
            return View(warranties);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var warranty = await _service.GetByIdAsync(id);
            if (warranty == null) return NotFound();
            return View(warranty);
        }

        public IActionResult Create()
        {
            return View(new WarrantyCreateVM { Items = { new WarrantyItemVM() } });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarrantyCreateVM model)
        {
            if (!ModelState.IsValid) return View(model);
            await _service.CreateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Print(int id)
        {
            var warranty = await _service.GetByIdAsync(id);

            if (warranty == null)
                return NotFound();

            return new ViewAsPdf("Print", warranty)
            {
                FileName = $"zemanet_{id}.pdf",

                PageSize = Rotativa.AspNetCore.Options.Size.A4,

                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,

                PageMargins = new Rotativa.AspNetCore.Options.Margins(10, 10, 10, 10)
            };
        }


    }
}