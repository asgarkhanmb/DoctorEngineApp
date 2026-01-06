namespace DoctorEngineApp.ViewModels.Technicians
{
    public class TechnicianEditVM
    {
        public int Id { get; set; }

        public string FullName { get; set; }
        public string Position { get; set; }

        public string ExistingImage { get; set; }
        public IFormFile Photo { get; set; }

        public string FacebookUrl { get; set; }
        public string InstagramUrl { get; set; }
        public string TiktokUrl { get; set; }
    }
}
