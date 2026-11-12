using DoctorEngineApp.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Categories
{
    public class CategoryCreateVM
    {
        [Required(ErrorMessage = "Kateqoriyanın adını daxil edin")]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "İkon seçin")]
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? IconFile { get; set; }

        // ===== Video (ixtiyari — sonra da əlavə etmək olar) =====

        [StringLength(150, ErrorMessage = "Başlıq maksimum 150 simvol ola bilər")]
        public string? VideoTitle { get; set; }

        [StringLength(1000, ErrorMessage = "Təsvir maksimum 1000 simvol ola bilər")]
        public string? VideoDescription { get; set; }

        [AllowedUpload(UploadKind.Video)]
        public IFormFile? VideoFile { get; set; }
    }
}
