namespace DoctorEngineApp.Models
{
    public class Service:BaseEntity
    {
        public string Name { get; set; }

        public ICollection<Reservation> Reservations { get; set; }
    }
}
