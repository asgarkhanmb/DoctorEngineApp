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
        public DbSet<WhatsappSetting> WhatsappSettings { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;   // tətbiqlərin giriş tokenləri
        public DbSet<Warranty> Warranties { get; set; }
        public DbSet<WarrantyItem> WarrantyItems { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; } = null!;

    }
}
