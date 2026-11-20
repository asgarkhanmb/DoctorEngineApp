using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Çat qrupu (ustalar yaradır). Qrupda hamı bir-birinin mesajlarını görür.
    /// Mesajlarda yazışma açarı: ChatMessage.ConversationUserName = "g:{Id}".
    /// </summary>
    [Index(nameof(LastMessageId))]
    [Index(nameof(DirectKey), IsUnique = true)]
    public class ChatGroup
    {
        public const string KeyPrefix = "g:";
        /// <summary>İki istifadəçinin şəxsi yazışması ("+" → istifadəçi seç): "d:{Id}".</summary>
        public const string DirectPrefix = "d:";

        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        /// <summary>Bütün istifadəçilər (yeniləri də avtomatik) üzvdür.</summary>
        public bool AllUsers { get; set; }

        /// <summary>Yalnız ustalar yaza bilər (elan kanalı).</summary>
        public bool AdminsOnly { get; set; }

        [MaxLength(256)]
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        public int LastMessageId { get; set; }
        public DateTime LastMessageDate { get; set; }

        /// <summary>
        /// Şəxsi yazışma (2 üzv): yalnız həmin iki istifadəçi görür — ustalar da görmür.
        /// Qrup kimi işləyir (✓✓, oxunmamış, bildiriş), amma siyahıda qarşı tərəfin adı ilə görünür.
        /// </summary>
        public bool IsDirect { get; set; }

        /// <summary>Şəxsi yazışmanın açarı: iki istifadəçi adı (kiçik hərflə, sıralı) "a|b" — təkrar yaranmasın.</summary>
        [MaxLength(520)]
        public string? DirectKey { get; set; }

        public string Key => (IsDirect ? DirectPrefix : KeyPrefix) + Id;
    }

    /// <summary>Qrup üzvü və onun oxuduğu / aldığı son mesaj.</summary>
    [Index(nameof(GroupId), nameof(UserName), IsUnique = true)]
    [Index(nameof(UserName))]
    public class ChatGroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>Açıq üzv (siyahıdan əlavə olunub). "Bütün istifadəçilər" qrupunda false ola bilər — yalnız oxuma göstəricisi üçün sətir.</summary>
        public bool IsExplicit { get; set; } = true;

        public int ReadId { get; set; }
        public int DeliveredId { get; set; }
        public DateTime JoinedDate { get; set; }
    }
}
