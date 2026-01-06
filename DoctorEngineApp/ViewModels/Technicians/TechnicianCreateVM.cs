using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Technicians
{
    public class TechnicianCreateVM
    {
        [Required]
        public string FullName { get; set; }
        [Required]
        public string Position { get; set; }
        public IFormFile Photo { get; set; }
        public string FacebookUrl { get; set; }
        public string InstagramUrl { get; set; }
        public string TiktokUrl { get; set; }
    }
}
