namespace DoctorEngineApp.ViewModels.ContactPhones
{
    using System.ComponentModel.DataAnnotations;

    public class ContactPhoneVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kiçik başlığı daxil edin")]
        public string Subtitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Başlığı daxil edin")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mətni daxil edin")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon nömrəsini daxil edin")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

}
