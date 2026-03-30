using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    public class ChatMessage :BaseEntity
    {
        [Required]
        public string Message { get; set; }

        public string UserName { get; set; }

        public bool IsEdited { get; set; } = false;
    }
}
