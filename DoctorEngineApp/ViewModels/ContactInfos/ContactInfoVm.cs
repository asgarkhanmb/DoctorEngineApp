using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DoctorEngineApp.ViewModels.ContactInfos
{
    public class ContactInfoVm
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string YouTube { get; set; }
        public string Facebook { get; set; }
        public string TikTok { get; set; }
        public string WhatsApp { get; set; }
        public string Weekdays { get; set; }
        public string Weekend { get; set; }
        [ValidateNever]
        public List<ServiceVM> Services { get; set; }

    }
}
