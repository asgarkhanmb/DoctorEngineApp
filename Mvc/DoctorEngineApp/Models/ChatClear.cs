using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>
    /// "Yazışmanı sil" (yalnız mənim üçün): istifadəçi bu yazışmanı <see cref="ClearedId"/>-yə qədər silib —
    /// həmin mesajlar ona görünmür, yeni mesaj gələndə yazışma siyahıda yenidən çıxır (WhatsApp kimi).
    /// </summary>
    [Index(nameof(UserName), nameof(Conversation), IsUnique = true)]
    public class ChatClear
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>Yazışma açarı (müştərinin adı və ya "g:{id}").</summary>
        [Required]
        [MaxLength(256)]
        public string Conversation { get; set; } = string.Empty;

        public int ClearedId { get; set; }
        public DateTime ClearedDate { get; set; }
    }
}
