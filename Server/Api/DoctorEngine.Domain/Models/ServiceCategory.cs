namespace DoctorEngineApp.Models
{
    public class ServiceCategory:BaseEntity
    {
        // Saytda göstərilən maksimum kateqoriya sayı (hər birində bir video)
        public const int MaxCount = 6;

        public string Name { get; set; } = null!;
        public string? Icon { get; set; }
        public List<SubService> SubServices { get; set; } = new();

    }
}
