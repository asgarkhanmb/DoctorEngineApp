using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class ContactPhone:BaseEntity
    {
        [Required]
        public string Subtitle { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string PhoneNumber { get; set; }
    }
}
