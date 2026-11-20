using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Müştəri ↔ ustalar yazışmasının vəziyyəti: son mesaj və hər tərəfin "çatdı" (✓✓) / "oxundu" (mavi ✓✓) göstəriciləri.
    /// Ustalar tərəfi ortaqdır — bir usta oxuyanda hamı üçün oxunmuş sayılır.
    /// </summary>
    [Index(nameof(UserName), IsUnique = true)]
    [Index(nameof(LastMessageId))]
    public class ChatConversation
    {
        public int Id { get; set; }

        /// <summary>Müştərinin istifadəçi adı.</summary>
        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        public int LastMessageId { get; set; }
        public DateTime LastMessageDate { get; set; }

        /// <summary>Müştərinin aldığı / oxuduğu son mesaj (ustaların mesajları üçün ✓✓).</summary>
        public int UserDeliveredId { get; set; }
        public int UserReadId { get; set; }

        /// <summary>Ustaların aldığı / oxuduğu son mesaj (müştərinin mesajları üçün ✓✓).</summary>
        public int AdminDeliveredId { get; set; }
        public int AdminReadId { get; set; }
    }
}
