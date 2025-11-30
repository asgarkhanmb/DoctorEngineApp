using Microsoft.AspNetCore.Identity;

namespace DoctorEngineApp.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; }
    } 
}
