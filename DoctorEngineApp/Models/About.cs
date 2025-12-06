using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class About :BaseEntity
    {
        [StringLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        // Stored relative to wwwroot, e.g. /uploads/about/uuid.mp4
        public string? VideoPath { get; set; }
    }
}
