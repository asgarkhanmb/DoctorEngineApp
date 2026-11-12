using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Users
{
    public class UserAddRoleVM
    {
        [Required(ErrorMessage = "İstifadəçi seçin")]
        public string? UsernameId { get; set; }

        [Required(ErrorMessage = "Rol seçin")]
        public string? RoleId { get; set; }
    }
}
