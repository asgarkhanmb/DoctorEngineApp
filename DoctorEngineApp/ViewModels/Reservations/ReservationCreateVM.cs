using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Services;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationCreateVM
    {
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string WhatsApp { get; set; }
        public int ServiceId { get; set; }
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime DateTime { get; set; } = DateTime.Now;
        public string Note { get; set; }

        public List<ServiceVM> Services { get; set; } = new List<ServiceVM>();
    }
}
