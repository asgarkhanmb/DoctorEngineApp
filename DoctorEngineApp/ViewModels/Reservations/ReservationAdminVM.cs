namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationAdminVM
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string WhatsApp { get; set; }

        public string ServiceName { get; set; }
        public string ServiceWhatsApp { get; set; }

        public DateTime DateTime { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string RejectionReason { get; set; }
    }
}
