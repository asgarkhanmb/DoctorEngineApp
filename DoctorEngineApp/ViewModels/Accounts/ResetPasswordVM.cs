using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Accounts
{
    public class ResetPasswordVM
    {
        [Required]
        [EmailAddress(ErrorMessage = "Email düzgün formatda deyil")]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }

        [Required(ErrorMessage = "Yeni şifrə mütləqdir")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Şifrə minimum 6 simvol olmalıdır")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Şifrə təkrarı mütləqdir")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Şifrələr eyni deyil")]
        public string ConfirmPassword { get; set; }
    }
}
