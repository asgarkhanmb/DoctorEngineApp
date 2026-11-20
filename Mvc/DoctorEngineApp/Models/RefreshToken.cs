using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Mobil / desktop tətbiqin uzun ömürlü giriş tokeni.
    /// Bazada tokenin özü yox, yalnız SHA-256 həşi saxlanılır (baza oğurlansa da tokenlər istifadə oluna bilməz).
    /// Hər yeniləmədə token dəyişir (rotation); köhnə token yenidən istifadə olunarsa — oğurluq sayılır
    /// və həmin cihazın bütün tokenləri ləğv edilir.
    /// </summary>
    [Index(nameof(TokenHash), IsUnique = true)]
    [Index(nameof(UserId))]
    [Index(nameof(FamilyId))]
    public class RefreshToken
    {
        public int Id { get; set; }

        [Required, MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>Eyni girişdən törənən tokenlər zənciri (oğurluq aşkarlananda hamısı ləğv olunur).</summary>
        [Required, MaxLength(32)]
        public string FamilyId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DeviceName { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }

        [MaxLength(64)]
        public string? ReplacedByHash { get; set; }

        public bool IsActive => RevokedAtUtc == null && DateTime.UtcNow < ExpiresAtUtc;
    }
}
