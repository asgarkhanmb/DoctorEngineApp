using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Models
{
    /// <summary>Mesaja reaksiya (❤️ 👍 😂 ...). Hər istifadəçinin bir mesaja bir reaksiyası olur.</summary>
    [Index(nameof(MessageId), nameof(UserName), IsUnique = true)]
    public class ChatReaction
    {
        public int Id { get; set; }

        public int MessageId { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(16)]
        public string Emoji { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }
    }
}
