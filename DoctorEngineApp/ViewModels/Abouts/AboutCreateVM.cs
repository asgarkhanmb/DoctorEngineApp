using DoctorEngineApp.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Abouts
{
    public class AboutCreateVM
    {
        [StringLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Video seçin.")]
        [AllowedUpload(UploadKind.Video)]
        public IFormFile? VideoFile { get; set; }
    }
}
