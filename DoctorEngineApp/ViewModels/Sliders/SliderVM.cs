using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Sliders
{
    public class SliderVM
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Subtitle { get; set; }

        public IFormFile? ImageFile1 { get; set; }
        public IFormFile? ImageFile2 { get; set; }

        public string? ExistingImage1 { get; set; }
        public string? ExistingImage2 { get; set; }
    }
}
