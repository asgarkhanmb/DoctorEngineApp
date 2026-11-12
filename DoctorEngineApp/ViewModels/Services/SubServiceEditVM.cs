using DoctorEngineApp.Helpers;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Services
{
    public class SubServiceEditVM
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        public string? ExistingVideo { get; set; }
        [AllowedUpload(UploadKind.Video)]
        public IFormFile? VideoFile { get; set; }

        [Required]
        public int CategoryId { get; set; }
    }
}
