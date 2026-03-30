using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.ContactInfos;
using DoctorEngineApp.ViewModels.ContactMessage;
using DoctorEngineApp.ViewModels.Maps;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DoctorEngineApp.ViewModels.ContactPhones
{
    public class ContactPageVM
    {
        [ValidateNever]
        public ContactInfoVm Contact { get; set; }
        [ValidateNever]
        public MapVM Map { get; set; }
        public ContactMessageCreateVM Message { get; set; }
        public List<ChatMessage> ChatMessages { get; set; }

    }
}
