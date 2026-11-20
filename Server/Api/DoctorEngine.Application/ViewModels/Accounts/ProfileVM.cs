using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Accounts
{
    // Profil səhifəsi: üç ayrı forma (məlumatlar, email, şifrə)
    public class ProfileVM
    {
        public ProfileInfoVM Info { get; set; } = new();
        public ChangeEmailVM Email { get; set; } = new();
        public ChangePasswordVM Password { get; set; } = new();

        // Hazırkı (təsdiqlənmiş) email — yalnız göstərmək üçün
        public string CurrentEmail { get; set; } = string.Empty;

        // Profil şəklinin fayl adı (yoxdursa null)
        public string? ProfileImage { get; set; }
    }

    public class ProfileInfoVM
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
    }

    public class ChangeEmailVM
    {
        [Required(ErrorMessage = "Yeni email ünvanını daxil edin")]
        [EmailAddress(ErrorMessage = "Email düzgün formatda deyil")]
        [Display(Name = "Yeni email")]
        public string NewEmail { get; set; } = string.Empty;

        // Hesab təhlükəsizliyi üçün email dəyişikliyi şifrə ilə təsdiqlənir
        [Required(ErrorMessage = "Hazırkı şifrəni daxil edin")]
        [DataType(DataType.Password)]
        [Display(Name = "Hazırkı şifrə")]
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Hazırkı şifrəni daxil edin")]
        [DataType(DataType.Password)]
        [Display(Name = "Hazırkı şifrə")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifrəni daxil edin")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifrə minimum 6 simvol olmalıdır")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifrə")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifrəni təkrar daxil edin")]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifrələr eyni deyil")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifrə təkrarı")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
