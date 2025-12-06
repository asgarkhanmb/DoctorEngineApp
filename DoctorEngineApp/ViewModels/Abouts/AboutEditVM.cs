using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Abouts
{
    public class AboutEditVM
    {
        public int Id { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? CurrentVideo { get; set; }

        public IFormFile? VideoFile { get; set; } // optional
    }
}
