using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Warranties
{
    public class WarrantyCreateVM
    {
        [Required(ErrorMessage = "Müştərinin adını daxil edin")]
        [StringLength(100)]
        public string CustomerFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Avtomobil modelini daxil edin")]
        [StringLength(100)]
        public string VehicleModel { get; set; } = string.Empty;

        [Range(1950, 2100, ErrorMessage = "İl düzgün deyil")]
        public int VehicleYear { get; set; } = DateTime.Now.Year;

        [Required(ErrorMessage = "Zəmanət şərtlərini daxil edin")]
        public string Terms { get; set; } = string.Empty;

        [MinLength(1, ErrorMessage = "Ən azı bir detal əlavə edin")]
        public List<WarrantyItemVM> Items { get; set; } = new List<WarrantyItemVM>();
    }
}
