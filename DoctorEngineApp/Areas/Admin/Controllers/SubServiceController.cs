using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    /// <summary>
    /// Kateqoriya videoları. Qayda: hər kateqoriyada bir video —
    /// yeni video yalnız boş kateqoriyaya əlavə oluna / köçürülə bilər.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SubServiceController : Controller
    {
        private readonly ISubServiceService _service;
        private readonly AppDbContext _ctx;

        public SubServiceController(ISubServiceService service, AppDbContext ctx)
        {
            _service = service;
            _ctx = ctx;
        }

        public async Task<IActionResult> Index() => View(await _service.GetAllAsync());

        public async Task<IActionResult> Create(int? categoryId = null)
        {
            await FillCategoriesAsync(null);
            return View(new SubServiceCreateVM { CategoryId = categoryId ?? 0 });
        }

        [HttpPost]
        public async Task<IActionResult> Create(SubServiceCreateVM model)
        {
            await CheckCategoryIsFreeAsync(model.CategoryId, exceptSubServiceId: null);
            if (!ModelState.IsValid)
            {
                await FillCategoriesAsync(null);
                return View(model);
            }

            await _service.CreateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _service.GetForEditAsync(id);
            if (vm == null) return NotFound();
            await FillCategoriesAsync(vm.CategoryId);
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SubServiceEditVM model)
        {
            var current = await _service.GetForEditAsync(model.Id);
            if (current == null) return NotFound();

            if (model.CategoryId != current.CategoryId)
                await CheckCategoryIsFreeAsync(model.CategoryId, exceptSubServiceId: model.Id);

            if (!ModelState.IsValid)
            {
                model.ExistingVideo = current.ExistingVideo;
                await FillCategoriesAsync(current.CategoryId);
                return View(model);
            }

            await _service.UpdateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Videonu boş kateqoriyaya köçürür (təkrarlanan videoları düzəltmək üçün).</summary>
        [HttpPost]
        public async Task<IActionResult> Move(int id, int categoryId, string? returnUrl = null)
        {
            var entity = await _ctx.SubServices.FindAsync(id);
            if (entity == null) return NotFound();

            var target = await _ctx.ServiceCategories.Include(c => c.SubServices).FirstOrDefaultAsync(c => c.Id == categoryId);
            if (target == null)
                TempData["Error"] = "Kateqoriya tapılmadı.";
            else if (target.SubServices.Any())
                TempData["Error"] = $"“{target.Name}” kateqoriyasının artıq videosu var.";
            else
            {
                entity.CategoryId = categoryId;
                await _ctx.SaveChangesAsync();
                TempData["Success"] = $"Video “{target.Name}” kateqoriyasına köçürüldü.";
            }

            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, string? returnUrl = null)
        {
            await _service.DeleteAsync(id);
            if (Url.IsLocalUrl(returnUrl)) { TempData["Success"] = "Video silindi."; return LocalRedirect(returnUrl!); }
            return RedirectToAction(nameof(Index));
        }

        // ================= KÖMƏKÇİLƏR =================

        /// <summary>Seçim üçün: videosu olmayan kateqoriyalar (+ redaktə olunan videonun öz kateqoriyası).</summary>
        private async Task FillCategoriesAsync(int? currentCategoryId)
        {
            ViewBag.Categories = await _ctx.ServiceCategories
                .Where(c => !c.SubServices.Any() || c.Id == currentCategoryId)
                .OrderBy(c => c.Id)
                .ToListAsync();
        }

        private async Task CheckCategoryIsFreeAsync(int categoryId, int? exceptSubServiceId)
        {
            if (categoryId <= 0)
            {
                ModelState.AddModelError("CategoryId", "Kateqoriya seçin");
                return;
            }

            var busy = await _ctx.SubServices.AnyAsync(s => s.CategoryId == categoryId && s.Id != exceptSubServiceId);
            if (busy)
                ModelState.AddModelError("CategoryId", "Bu kateqoriyanın artıq videosu var. Hər kateqoriyada bir video ola bilər — mövcud videonu kateqoriyanın səhifəsindən dəyişin.");
        }
    }
}
