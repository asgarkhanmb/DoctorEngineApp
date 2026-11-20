using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class Slider : BaseEntity
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>Fon şəkli (məcburi). Tövsiyə: 1920×900 və ya daha böyük.</summary>
        public string Image1 { get; set; } = string.Empty;

        /// <summary>Sağ tərəfdəki əlavə şəkil (ixtiyari, şəffaf PNG tövsiyə olunur).</summary>
        public string? Image2 { get; set; }
    }
}
