namespace DoctorEngineApp.Models
{
    public class SubService:BaseEntity
    {
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Video { get; set; }
        public int CategoryId { get; set; }
        public ServiceCategory? Category { get; set; }

    }
}
