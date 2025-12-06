using DoctorEngineApp.ViewModels.Infroms;

namespace DoctorEngineApp.ViewModels.Abouts
{
    public class AboutVM
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string VideoPath { get; set; }
        public List<InformVM> Informs { get; set; }
    }
}
