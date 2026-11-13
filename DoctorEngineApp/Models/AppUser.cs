using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; }

        // Profil şəkli: wwwroot/uploads/avatars/{fayl adı}
        [MaxLength(300)]
        public string? ProfileImage { get; set; }

        // Çatda istifadəçinin oxuduğu son mesajın Id-si (oxunmamış bildirişləri üçün)
        public int? LastReadChatMessageId { get; set; }
    }
}
