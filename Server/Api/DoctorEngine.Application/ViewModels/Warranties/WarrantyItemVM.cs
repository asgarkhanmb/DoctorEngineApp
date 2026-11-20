using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Warranties
{
    public class WarrantyItemVM
    {
        [Required(ErrorMessage = "Detalın adını daxil edin")]
        [StringLength(100)]
        public string PartName { get; set; } = string.Empty;

        [Range(1, 120, ErrorMessage = "Müddət 1–120 ay olmalıdır")]
        public int WarrantyMonth { get; set; } = 6;
    }
}
