namespace DoctorEngineApp.Models
{
    public class ServiceCategory:BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Icon { get; set; }
        public List<SubService> SubServices { get; set; } = new();

    }
}
