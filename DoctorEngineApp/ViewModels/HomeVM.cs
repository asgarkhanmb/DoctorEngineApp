using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Services;
using DoctorEngineApp.ViewModels.Sliders;

namespace DoctorEngineApp.ViewModels
{
    public class HomeVM
    {
        public List<SliderVM> Sliders { get; set; }
        public List<InformVM> Informs { get; set; }
        public List<AboutVM> Abouts { get; set; }
        public List<CategoryVM> Categories { get; set; }
        public List<SubServiceVM>SubServices { get; set; }

    }
}
