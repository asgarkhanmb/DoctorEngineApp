using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Azərbaycanca mətnin AI ilə edilmiş tərcüməsi (en / ru / tr).
    /// Açar — mənbə mətnin SHA-256 hash-i + dil: admin mətni dəyişəndə hash dəyişir və yeni tərcümə avtomatik yaranır.
    /// </summary>
    public class ContentTranslation
    {
        public int Id { get; set; }

        [MaxLength(64)]
        public string SourceHash { get; set; } = null!;

        [MaxLength(5)]
        public string Lang { get; set; } = null!;

        public string Source { get; set; } = null!;
        public string Value { get; set; } = null!;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
