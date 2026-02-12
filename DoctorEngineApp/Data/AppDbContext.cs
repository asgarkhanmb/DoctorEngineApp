using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options):base(options){}


        public DbSet<Slider> Sliders { get; set; }
        public DbSet<Inform>Informs { get; set; }
        public DbSet<About> Abouts { get; set; }
        public DbSet<ServiceCategory> ServiceCategories { get; set; } = null!;
        public DbSet<SubService> SubServices { get; set; } = null!;
        public DbSet<Service>Services { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Technician> Technicians { get; set; }
        public DbSet<ContactInfo> ContactInfos { get; set; }
        public DbSet<ContactPhone>ContactPhones { get; set; }
        public DbSet<Map>Maps { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<JobCatalog>JobCatalogs { get; set; }
        public DbSet<Warranty> Warranties { get; set; }
        public DbSet<WarrantyJob> WarrantiesJobs { get; set; }
        public DbSet<WarrantyLogo>WarrantyLogos { get; set; }


}
}
