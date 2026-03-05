using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Accounts
{
    public class ForgotPasswordVM
    {
        [Required(ErrorMessage = "Email mütləqdir")]
        [EmailAddress(ErrorMessage = "Email düzgün formatda deyil")]
        public string Email { get; set; }
    }
}
