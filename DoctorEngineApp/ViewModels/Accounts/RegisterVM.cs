using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Accounts
{
    public class RegisterVM
    {
        [Required(ErrorMessage = "Ad və soyadı daxil edin")]
        [StringLength(100, ErrorMessage = "Ad Soyad maksimum 100 simvol ola bilər")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "İstifadəçi adını daxil edin")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "İstifadəçi adı 3–30 simvol olmalıdır")]
        [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Yalnız latın hərfləri, rəqəm və . _ - istifadə edin")]
        [Display(Name = "İstifadəçi adı")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email ünvanını daxil edin")]
        [EmailAddress(ErrorMessage = "Email düzgün formatda deyil")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifrəni daxil edin")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifrə minimum 6 simvol olmalıdır")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrə")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifrəni təkrar daxil edin")]
        [Compare(nameof(Password), ErrorMessage = "Şifrələr eyni deyil")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrə təkrarı")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
