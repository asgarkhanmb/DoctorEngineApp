using System.ComponentModel.DataAnnotations;
using DoctorEngineApp.Helpers;
namespace DoctorEngineApp.ViewModels.Technicians
{
    public class TechnicianEditVM
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Ad Soyadı daxil edin")] public string FullName { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vəzifəni daxil edin")] public string Position { get; set; } = string.Empty;
        [StringLength(300, ErrorMessage = "Təqdimat maksimum 300 simvol ola bilər")] public string? Bio { get; set; }

        public string? ExistingImage { get; set; }
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? Photo { get; set; }

        [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")] public string? FacebookUrl { get; set; }
        [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")] public string? InstagramUrl { get; set; }
        [System.ComponentModel.DataAnnotations.Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")] public string? TiktokUrl { get; set; }
    }
}
