namespace DoctorEngineApp.Models
{
    public class Technician :BaseEntity
    {
        public string FullName { get; set; }
        public string Position { get; set; }

        public string Image { get; set; }

        public string FacebookUrl { get; set; }
        public string InstagramUrl { get; set; }
        public string TiktokUrl { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
