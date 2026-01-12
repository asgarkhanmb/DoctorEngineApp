using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class Reservation:BaseEntity
    {
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string WhatsApp { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }
        [Required]
        public DateTime DateTime { get; set; }
        public string Note { get; set; }

        public string Status { get; set; } = "Pending";
        public string? RejectionReason { get; set; }

    }
}
