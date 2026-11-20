using DoctorEngineApp.ViewModels.Categories;

namespace DoctorEngineApp.ViewModels.Services
{
    public class ServicePageVM
    {
        public List<CategoryVM> Categories { get; set; }
        public List<ServiceVM>Services {  get; set; }
        public List<SubServiceVM> SubServices { get; set; } = new();
    }
}
