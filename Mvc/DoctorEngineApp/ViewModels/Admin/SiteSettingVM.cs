using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Admin
{
    public class SiteSettingVM
    {
        [Required(ErrorMessage = "Saytın adını daxil edin")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "Ad 2–60 simvol olmalıdır")]
        public string SiteName { get; set; } = SiteDefaults.Name;

        [Required(ErrorMessage = "İkon seçin")]
        public string LogoIcon { get; set; } = SiteDefaults.Icon;

        /// <summary>Yeni loqo şəkli (ixtiyari). Seçilərsə ikonun yerinə göstərilir.</summary>
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? LogoFile { get; set; }

        public bool RemoveLogo { get; set; }

        public string? ExistingLogo { get; set; }
    }
}
