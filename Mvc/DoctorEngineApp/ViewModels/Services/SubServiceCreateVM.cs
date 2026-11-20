using DoctorEngineApp.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Services
{
    public class SubServiceCreateVM
    {
        [Required]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        [AllowedUpload(UploadKind.Video)]
        public IFormFile VideoFile { get; set; } = null!;

        [Required]
        public int CategoryId { get; set; }
    }
}
