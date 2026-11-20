using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class ContactInfo :BaseEntity    
    {
        [Required, MaxLength(250)]
        public string Address { get; set; }

        [Required, MaxLength(50)]
        public string Phone { get; set; }

        [Required, MaxLength(100)]
        public string Email { get; set; }

        [MaxLength(200)]
        public string YouTube { get; set; }

        [MaxLength(200)]
        public string Facebook { get; set; }

        [MaxLength(200)]
        public string TikTok { get; set; }

        [MaxLength(50)]
        public string WhatsApp { get; set; }

        [MaxLength(100)]
        public string Weekdays { get; set; }
        [MaxLength(100)]
        public string Weekend { get; set; }
    }
}
