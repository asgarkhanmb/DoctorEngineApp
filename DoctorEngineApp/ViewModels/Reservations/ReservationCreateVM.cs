using DoctorEngineApp.Helpers;
using DoctorEngineApp.ViewModels.Services;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationCreateVM
    {
        [Required(ErrorMessage = "Ad Soyad tələb olunur")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon tələb olunur")]
        [RegularExpression(@"^\+?[0-9\s\-()]{7,20}$", ErrorMessage = "Telefon nömrəsi düzgün deyil")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "WhatsApp nömrəsi tələb olunur")]
        [RegularExpression(@"^\+?[0-9\s\-()]{7,20}$", ErrorMessage = "WhatsApp nömrəsi düzgün deyil")]
        public string WhatsApp { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Xidmət seçin")]
        public int ServiceId { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime DateTime { get; set; } = AzTime.Now;

        // Qeyd məcburi deyil
        [StringLength(1000)]
        public string? Note { get; set; }

        public List<ServiceVM> Services { get; set; } = new List<ServiceVM>();
    }
}
