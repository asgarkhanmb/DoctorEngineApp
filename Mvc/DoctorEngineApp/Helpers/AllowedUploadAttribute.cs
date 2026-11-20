using DoctorEngineApp.Localization;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Helpers
{
    public enum UploadKind
    {
        Image,
        Video,
        Audio   // çatda səs mesajları
    }

    /// <summary>
    /// Yüklənən faylın uzantısını, MIME tipini, həqiqi məzmununu (magic bytes) və ölçüsünü yoxlayır.
    /// Yalnız icazə verilən formatlar wwwroot-a yazıla bilər (məs. .html/.svg/.exe yox).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AllowedUploadAttribute : ValidationAttribute
    {
        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        public static readonly string[] VideoExtensions = { ".mp4", ".webm", ".mov", ".m4v", ".ogg" };
        public static readonly string[] AudioExtensions = { ".webm", ".ogg", ".m4a", ".mp3" };

        public const long DefaultMaxImageBytes = 10L * 1024 * 1024;      // 10 MB
        public const long DefaultMaxVideoBytes = 1024L * 1024 * 1024;    // 1 GB
        public const long DefaultMaxAudioBytes = 16L * 1024 * 1024;      // 16 MB

        private readonly UploadKind _kind;

        public AllowedUploadAttribute(UploadKind kind)
        {
            _kind = kind;
        }

        /// <summary>Faylın şəkil, yoxsa video olduğunu uzantıya görə müəyyən edir (heç biri deyilsə null).</summary>
        public static UploadKind? DetectKind(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            // .webm/.ogg həm səs, həm video ola bilər — səs Content-Type ilə ayrılır
            if (AudioExtensions.Contains(ext) &&
                (file.ContentType ?? "").StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                return UploadKind.Audio;
            if (ImageExtensions.Contains(ext)) return UploadKind.Image;
            if (VideoExtensions.Contains(ext)) return UploadKind.Video;
            return null;
        }

        public static bool IsAllowed(IFormFile file, UploadKind kind, out string error, long? maxBytesOverride = null)
        {
            error = string.Empty;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            var (extensions, mimePrefix, maxBytes, label) = kind switch
            {
                UploadKind.Image => (ImageExtensions, "image/", DefaultMaxImageBytes, L.T("Şəkil")),
                UploadKind.Audio => (AudioExtensions, "audio/", DefaultMaxAudioBytes, L.T("Səs")),
                _ => (VideoExtensions, "video/", DefaultMaxVideoBytes, L.T("Video"))
            };

            if (maxBytesOverride.HasValue)
                maxBytes = maxBytesOverride.Value;

            if (!extensions.Contains(ext))
            {
                error = L.T("{0} formatı düzgün deyil. İcazə verilən: {1}", label, string.Join(", ", extensions));
                return false;
            }

            if (string.IsNullOrEmpty(file.ContentType) ||
                !file.ContentType.StartsWith(mimePrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = L.T("{0} faylı seçin.", label);
                return false;
            }

            if (file.Length == 0)
            {
                error = L.T("Fayl boşdur.");
                return false;
            }

            if (file.Length > maxBytes)
            {
                error = L.T("{0} ölçüsü maksimum {1} MB ola bilər.", label, maxBytes / (1024 * 1024));
                return false;
            }

            if (!FileSignature.Matches(file, ext))
            {
                error = L.T("Fayl həqiqi {0} deyil və ya zədələnib.", label.ToLower(L.Culture));
                return false;
            }

            return true;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Boş sahə burada yoxlanmır — məcburidirsə [Required] ayrıca qoyulur
            if (value is not IFormFile file)
                return ValidationResult.Success;

            return IsAllowed(file, _kind, out var error)
                ? ValidationResult.Success
                : new ValidationResult(error, new[] { validationContext.MemberName ?? string.Empty });
        }
    }
}
