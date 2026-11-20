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
        public DbSet<ChatConversation> ChatConversations { get; set; } = null!;   // şəxsi yazışmalar (✓✓ vəziyyəti)
        public DbSet<ChatReaction> ChatReactions { get; set; } = null!;           // mesaj reaksiyaları
        public DbSet<ChatGroup> ChatGroups { get; set; } = null!;                 // çat qrupları
        public DbSet<ChatGroupMember> ChatGroupMembers { get; set; } = null!;     // qrup üzvləri (✓✓ vəziyyəti)
        public DbSet<ChatClear> ChatClears { get; set; } = null!;                 // "yazışmanı sil" (yalnız mənim üçün)
        public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;   // tətbiqlərin giriş tokenləri
        public DbSet<Warranty> Warranties { get; set; }
        public DbSet<WarrantyItem> WarrantyItems { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; } = null!;
        public DbSet<ContentTranslation> ContentTranslations { get; set; } = null!;   // AI tərcümələri (en/ru/tr)
        public DbSet<Reel> Reels { get; set; } = null!;                     // "Videolar" lenti (Reels)
        public DbSet<ReelLike> ReelLikes { get; set; } = null!;
        public DbSet<ReelComment> ReelComments { get; set; } = null!;
        public DbSet<ReelCommentLike> ReelCommentLikes { get; set; } = null!;   // rəy bəyənmələri
        public DbSet<AppNotification> Notifications { get; set; } = null!;   // tətbiq daxili bildirişlər (bəyənmə, rəy, cavab)

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ContentTranslation>()
                .HasIndex(t => new { t.SourceHash, t.Lang })
                .IsUnique();
        }
    }
}
