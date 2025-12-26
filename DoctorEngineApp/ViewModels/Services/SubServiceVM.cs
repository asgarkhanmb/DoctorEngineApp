using DoctorEngineApp.Models;

namespace DoctorEngineApp.ViewModels.Services
{
    public class SubServiceVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Video { get; set; }
        public string? CategoryName { get; set; }
        public List<Inform> Informs { get; set; } = new List<Inform>();
    }
}
