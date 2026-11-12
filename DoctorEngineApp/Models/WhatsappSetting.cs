using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class WhatsappSetting :BaseEntity
    {
        [Required(ErrorMessage = "WhatsApp nömrəsini daxil edin")]
        [RegularExpression(@"^\+?[0-9\s]{7,20}$", ErrorMessage = "Nömrə düzgün deyil")]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required(ErrorMessage = "Hazır mesajı daxil edin")]
        public string Message { get; set; } = string.Empty;
    }
}
