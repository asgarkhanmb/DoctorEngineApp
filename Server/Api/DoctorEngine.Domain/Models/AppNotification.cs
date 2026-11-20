using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Tətbiq daxili bildiriş (Instagram-dakı "ürək" bölməsi kimi): videonuzu bəyəndi, videonuza rəy yazdı, rəyinizə cavab verdi.
    /// Mətn tətbiqdə Type-a görə seçilmiş dildə qurulur; Snippet — rəyin qısa mətni.
    /// </summary>
    [Index(nameof(UserName), nameof(Id))]
    [Index(nameof(UserName), nameof(IsRead))]
    public class AppNotification
    {
        public int Id { get; set; }

        /// <summary>Bildirişi alan.</summary>
        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>Əməliyyatı edən.</summary>
        [Required]
        [MaxLength(256)]
        public string ActorUserName { get; set; } = string.Empty;

        /// <summary>reel_like | reel_comment | comment_reply | comment_like</summary>
        [Required]
        [MaxLength(32)]
        public string Type { get; set; } = string.Empty;

        public int? ReelId { get; set; }
        public int? CommentId { get; set; }

        [MaxLength(200)]
        public string? Snippet { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
