using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoryController : Controller
    {
        private readonly ICategoryService _service;
        public CategoryController(ICategoryService service) { _service = service; }

        public async Task<IActionResult> Index() => View(await _service.GetAllAsync());

        public async Task<IActionResult> Create()
        {
            if (await LimitReachedAsync()) return RedirectToAction(nameof(Index));
            return View(new CategoryCreateVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CategoryCreateVM model)
        {
            // Limit server tərəfdə də yoxlanılır (düymə gizli olsa da birbaşa sorğu göndərilə bilər)
            if (await LimitReachedAsync()) return RedirectToAction(nameof(Index));

            ValidateVideo(model.VideoFile != null, hasExisting: false, removing: false, model.VideoTitle, model.VideoDescription);
            if (!ModelState.IsValid) return View(model);

            await _service.CreateAsync(model);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _service.GetForEditAsync(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CategoryEditVM model)
        {
            var current = await _service.GetForEditAsync(model.Id);
            if (current == null) return NotFound();

            ValidateVideo(model.VideoFile != null, hasExisting: current.VideoId != null, removing: model.RemoveVideo,
                          model.VideoTitle, model.VideoDescription);

            if (!ModelState.IsValid)
            {
                // Forma yenidən göstəriləndə mövcud fayllar/siyahılar itməsin
                model.ExistingIcon = current.ExistingIcon;
                model.ExistingVideo = current.ExistingVideo;
                model.VideoId = current.VideoId;
                model.ExtraVideos = current.ExtraVideos;
                model.EmptyCategories = current.EmptyCategories;
                return View(model);
            }

            await _service.UpdateAsync(model);
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Maksimum kateqoriya sayına çatılıbsa, xəbərdarlıq yazır və true qaytarır.</summary>
        private async Task<bool> LimitReachedAsync()
        {
            if (await _service.CountAsync() < ServiceCategory.MaxCount) return false;

            TempData["Error"] = $"Maksimum {ServiceCategory.MaxCount} kateqoriya ola bilər. Yenisini əlavə etmək üçün əvvəlcə birini silin.";
            return true;
        }

        /// <summary>Video qaydaları: video olacaqsa başlıq və təsvir məcburidir.</summary>
        private void ValidateVideo(bool newFile, bool hasExisting, bool removing, string? title, string? description)
        {
            var willHaveVideo = !removing && (newFile || hasExisting);

            if (willHaveVideo)
            {
                if (string.IsNullOrWhiteSpace(title))
                    ModelState.AddModelError("VideoTitle", "Videonun başlığını daxil edin");
                if (string.IsNullOrWhiteSpace(description))
                    ModelState.AddModelError("VideoDescription", "Videonun təsvirini daxil edin");
            }
            else if (!removing && (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(description)))
            {
                ModelState.AddModelError("VideoFile", "Başlıq yazmısınız — video faylını da seçin");
            }
        }
    }
}
