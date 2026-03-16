using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class WhatsappSetting :BaseEntity
    {
        [Required]
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
    }
}
