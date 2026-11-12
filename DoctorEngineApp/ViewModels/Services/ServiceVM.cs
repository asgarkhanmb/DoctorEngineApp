using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Services
{
    public class ServiceVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Xidmətin adını daxil edin")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
