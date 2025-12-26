namespace DoctorEngineApp.Models
{
    public class Service:BaseEntity
    {
        public string Name { get; set; }
        public string WhatsApp { get; set; } // Servisin WhatsApp nömrəsi

        public ICollection<Reservation> Reservations { get; set; }
    }
}
