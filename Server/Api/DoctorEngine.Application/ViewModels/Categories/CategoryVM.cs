using DoctorEngineApp.ViewModels.Services;

namespace DoctorEngineApp.ViewModels.Categories
{
    public class CategoryVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Icon { get; set; }
        public List<SubServiceVM> SubServices { get; set; } = new();
    }
}
