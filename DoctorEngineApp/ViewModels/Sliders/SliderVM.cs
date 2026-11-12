using DoctorEngineApp.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Sliders
{
    public class SliderVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kiçik başlığı daxil edin")]
        [StringLength(60, ErrorMessage = "Kiçik başlıq maksimum 60 simvol ola bilər")]
        [Display(Name = "Kiçik başlıq")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Əsas başlığı daxil edin")]
        [StringLength(120, ErrorMessage = "Əsas başlıq maksimum 120 simvol ola bilər")]
        [Display(Name = "Əsas başlıq")]
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>Fon şəkli — yaradarkən məcburidir (controller yoxlayır).</summary>
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? ImageFile1 { get; set; }

        /// <summary>Sağ tərəf şəkli — ixtiyari.</summary>
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? ImageFile2 { get; set; }

        public bool RemoveImage2 { get; set; }

        public string? ExistingImage1 { get; set; }
        public string? ExistingImage2 { get; set; }
    }
}
