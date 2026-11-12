using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Accounts
{
    public class LoginVM
    {
        [Required(ErrorMessage = "Email və ya istifadəçi adını daxil edin")]
        [Display(Name = "Email və ya istifadəçi adı")]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifrəni daxil edin")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrə")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Məni xatırla")]
        public bool RememberMe { get; set; }
    }
}
