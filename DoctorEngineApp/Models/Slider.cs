using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class Slider:BaseEntity
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Subtitle { get; set; }

        public string Image1 { get; set; }
        public string Image2 { get; set; }
    }
}
