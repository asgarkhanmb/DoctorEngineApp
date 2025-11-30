using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<Slider> Sliders { get; set; }
    }
}
