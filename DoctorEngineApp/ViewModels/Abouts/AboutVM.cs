using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Infroms;

namespace DoctorEngineApp.ViewModels.Abouts
{
    public class AboutVM
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string VideoPath { get; set; }
        public List<InformVM> Informs { get; set; }
        public List<About> Abouts { get; internal set; }
        public List<Technician> Technicians { get; set; }

        // Telefon / ünvan — səhifədəki əlaqə sətri və çağırış bloku üçün
        public DoctorEngineApp.ViewModels.ContactInfos.ContactInfoVm? Contact { get; set; }
    }
}
