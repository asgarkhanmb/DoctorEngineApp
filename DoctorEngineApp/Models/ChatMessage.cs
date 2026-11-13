using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class ChatMessage : BaseEntity
    {
        // Mətn məcburi deyil: yalnız şəkil/video da göndərilə bilər
        [MaxLength(1000)]
        public string? Message { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        // Göndərən admin (usta) idisə — çatda "Usta" nişanı göstərilir
        public bool SenderIsAdmin { get; set; }

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
