using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.ContactMessage
{
    public class ContactMessageCreateVM
    {
        [Required(ErrorMessage = "Ad boş ola bilməz")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email boş ola bilməz")]
        [EmailAddress(ErrorMessage = "Email düzgün deyil")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mövzu boş ola bilməz")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Mesaj boş ola bilməz")]
        public string Message { get; set; }
    }
}
