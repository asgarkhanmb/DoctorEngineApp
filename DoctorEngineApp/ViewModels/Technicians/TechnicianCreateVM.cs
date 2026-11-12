using DoctorEngineApp.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Technicians
{
    public class TechnicianCreateVM
    {
        [Required(ErrorMessage = "Ad Soyadı daxil edin")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Vəzifəni daxil edin")]
        public string Position { get; set; }
        [Required(ErrorMessage = "Şəkil seçin")]
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? Photo { get; set; }
        [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")] public string? FacebookUrl { get; set; }
        [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")] public string? InstagramUrl { get; set; }
        [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")] public string? TiktokUrl { get; set; }
    }
}
