using DoctorEngineApp.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Categories
{
    public class CategoryEditVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kateqoriyanın adını daxil edin")]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        public string? ExistingIcon { get; set; }

        [AllowedUpload(UploadKind.Image)]
        public IFormFile? IconFile { get; set; }

        /// <summary>Hazır ikonun açarı (bax: CategoryIconPresets). Boşdursa ikon dəyişmir.</summary>
        public string? PresetIcon { get; set; }

        // ===== Kateqoriyanın videosu (hər kateqoriyada bir video) =====

        /// <summary>Mövcud video qeydi (SubService). Yoxdursa null.</summary>
        public int? VideoId { get; set; }

        [StringLength(150, ErrorMessage = "Başlıq maksimum 150 simvol ola bilər")]
        public string? VideoTitle { get; set; }

        [StringLength(1000, ErrorMessage = "Təsvir maksimum 1000 simvol ola bilər")]
        public string? VideoDescription { get; set; }

        [AllowedUpload(UploadKind.Video)]
        public IFormFile? VideoFile { get; set; }

        public string? ExistingVideo { get; set; }

        public bool RemoveVideo { get; set; }

        /// <summary>Bu kateqoriyadakı artıq videolar (köhnə məlumat) — saytda göstərilmir.</summary>
        [ValidateNever]
        public List<ExtraVideoVM> ExtraVideos { get; set; } = new();

        /// <summary>Videosu olmayan digər kateqoriyalar (artıq videonu köçürmək üçün).</summary>
        [ValidateNever]
        public List<SelectListItem> EmptyCategories { get; set; } = new();
    }

    public class ExtraVideoVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Video { get; set; }
    }
}
