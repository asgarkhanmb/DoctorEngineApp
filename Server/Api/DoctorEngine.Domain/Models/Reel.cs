using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// "Videolar" lenti (Instagram Reels kimi): admin video paylaşır, istifadəçilər bəyənir və rəy yazır.
    /// Fayl: wwwroot/uploads/reels/{VideoPath}. Say sahələri (LikeCount, CommentCount) lent sürətli açılsın deyə saxlanılır.
    /// </summary>
    [Index(nameof(SoftDeleted), nameof(Id))]
    public class Reel : BaseEntity
    {
        [Required]
        [MaxLength(300)]
        public string VideoPath { get; set; } = string.Empty;

        [MaxLength(2200)]
        public string? Caption { get; set; }

        /// <summary>Paylaşan adminin istifadəçi adı.</summary>
        [Required]
        [MaxLength(256)]
        public string AuthorUserName { get; set; } = string.Empty;

        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ViewCount { get; set; }
    }

    /// <summary>Bəyənmə — hər istifadəçi bir videonu bir dəfə bəyənir.</summary>
    [Index(nameof(ReelId), nameof(UserName), IsUnique = true)]
    public class ReelLike
    {
        public int Id { get; set; }
        public int ReelId { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>Rəyin bəyənilməsi — hər istifadəçi bir rəyi bir dəfə bəyənir.</summary>
    [Index(nameof(CommentId), nameof(UserName), IsUnique = true)]
    public class ReelCommentLike
    {
        public int Id { get; set; }
        public int CommentId { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>Videoya yazılan rəy (mətn və ya stiker). ParentId — başqa rəyə cavabdırsa.</summary>
    [Index(nameof(ReelId), nameof(Id))]
    [Index(nameof(ParentId))]
    public class ReelComment
    {
        public int Id { get; set; }
        public int ReelId { get; set; }

        /// <summary>Cavab verilən (yuxarı səviyyəli) rəy. Cavablar həmişə yuxarı səviyyəli rəyə bağlanır (Instagram kimi).</summary>
        public int? ParentId { get; set; }

        /// <summary>Cavab yazılan istifadəçi (cavaba cavabda "@ad" göstərmək üçün).</summary>
        [MaxLength(256)]
        public string? ReplyToUserName { get; set; }

        /// <summary>Stiker kodu (məs. "fire") — mətn əvəzinə.</summary>
        [MaxLength(32)]
        public string? Sticker { get; set; }

        public DateTime? EditedDate { get; set; }

        /// <summary>Rəyin bəyənmə sayı (ReelCommentLikes-dan hesablanır).</summary>
        public int LikeCount { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Text { get; set; } = string.Empty;   // stikerdə boş ola bilər

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
