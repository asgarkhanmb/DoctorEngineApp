using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Services;

namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationCreateVM
    {
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string WhatsApp { get; set; }

        public int ServiceId { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;
        public string Note { get; set; }

        public List<ServiceVM> Services { get; set; } = new List<ServiceVM>();
    }
}
