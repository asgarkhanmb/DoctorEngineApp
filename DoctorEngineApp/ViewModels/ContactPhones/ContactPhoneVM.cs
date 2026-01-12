namespace DoctorEngineApp.ViewModels.ContactPhones
{
    using System.ComponentModel.DataAnnotations;

    public class ContactPhoneVM
    {
        public int Id { get; set; }

        [Required]
        public string Subtitle { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string PhoneNumber { get; set; }
    }

}
