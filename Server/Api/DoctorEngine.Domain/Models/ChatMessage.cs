using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Çat mesajı. Hər müştərinin ustalarla (adminlərlə) ayrıca, şəxsi yazışması var (WhatsApp kimi):
    /// <see cref="ConversationUserName"/> — yazışmanın sahibi olan müştərinin istifadəçi adı.
    /// </summary>
    [Index(nameof(ConversationUserName), nameof(Id))]
    public class ChatMessage : BaseEntity
    {
        // Mətn məcburi deyil: yalnız şəkil/video da göndərilə bilər
        [MaxLength(1000)]
        public string? Message { get; set; }

        /// <summary>Göndərən.</summary>
        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>Yazışma: müştərinin istifadəçi adı (müştəri yazanda = UserName, usta yazanda = cavab verdiyi müştəri).</summary>
        [MaxLength(256)]
        public string? ConversationUserName { get; set; }

        // Göndərən admin (usta) idisə — çatda "Usta" nişanı göstərilir
        public bool SenderIsAdmin { get; set; }

        /// <summary>Cavab verilən mesaj (eyni yazışmadan).</summary>
        public int? ReplyToId { get; set; }

        // Əlavə fayl: /uploads/chat/{guid}.ext
        [MaxLength(300)]
        public string? AttachmentUrl { get; set; }

        // "image", "video" və ya "audio" (səs mesajı)
        [MaxLength(10)]
        public string? AttachmentType { get; set; }

        public bool IsEdited { get; set; } = false;
        public DateTime? EditedDate { get; set; }
    }
}
