using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Sliders;

namespace DoctorEngineApp.ViewModels
{
    public class HomeVM
    {
        public List<SliderVM> Sliders { get; set; }
        public List<InformVM> Informs { get; set; } = new List<InformVM>();
    }
}
