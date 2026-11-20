using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// Brauzerin Web Push abunəliyi: sayt bağlı / arxa planda olanda da
    /// yeni çat mesajı barədə bildiriş (və səs) göndərmək üçün.
    /// Bir istifadəçinin bir neçə cihazı (brauzeri) ola bilər.
    /// </summary>
    public class PushSubscription : BaseEntity
    {
        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        // Brauzer push serverinin ünvanı (hər cihaz üçün unikal)
        [Required]
        [MaxLength(800)]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Auth { get; set; } = string.Empty;

        // Bildirişin dili (az/en/ru/tr) — saytda seçilmiş dil; hər açılışda abunəlik yenilənəndə güncəllənir
        [Required]
        [MaxLength(5)]
        public string Lang { get; set; } = "az";
    }
}
