namespace DoctorEngineApp.ViewModels.Infroms
{
    public class InformEditVM
    {
        public int Id { get; set; }

        public string? CurrentIcon { get; set; }
        public IFormFile? Icon { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }
    }
}
