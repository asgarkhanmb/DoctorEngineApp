using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Chats
{
    /// <summary>Yeni mesaj (multipart/form-data): mətn və/və ya bir şəkil/video.</summary>
    public class ChatSendVM
    {
        [StringLength(1000, ErrorMessage = "Mesaj maksimum 1000 simvol ola bilər.")]
        public string? Message { get; set; }

        public IFormFile? File { get; set; }
    }

    /// <summary>Mesajın redaktəsi: mətni dəyiş, faylı sil və ya yenisi ilə əvəz et.</summary>
    public class ChatEditVM
    {
        [Range(1, int.MaxValue)]
        public int Id { get; set; }

        [StringLength(1000, ErrorMessage = "Mesaj maksimum 1000 simvol ola bilər.")]
        public string? Message { get; set; }

        public bool RemoveAttachment { get; set; }

        public IFormFile? File { get; set; }
    }

    /// <summary>Brauzerə və SignalR-a göndərilən mesaj.</summary>
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Message { get; set; }
        public bool IsAdmin { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public bool IsEdited { get; set; }
        /// <summary>Bakı vaxtı, "yyyy-MM-dd"</summary>
        public string Date { get; set; } = string.Empty;
        /// <summary>Bakı vaxtı, "HH:mm"</summary>
        public string Time { get; set; } = string.Empty;
    }

    public class ChatPageDto
    {
        public List<ChatMessageDto> Messages { get; set; } = new();
        public bool HasMore { get; set; }
    }

    /// <summary>Servis əməliyyatının nəticəsi.</summary>
    public class ChatResult
    {
        public ChatResultStatus Status { get; init; }
        public string? Error { get; init; }
        public ChatMessageDto? Message { get; init; }

        public static ChatResult Ok(ChatMessageDto msg) => new() { Status = ChatResultStatus.Ok, Message = msg };
        public static ChatResult Invalid(string error) => new() { Status = ChatResultStatus.Invalid, Error = error };
        public static ChatResult NotFound() => new() { Status = ChatResultStatus.NotFound };
    }

    public enum ChatResultStatus { Ok, Invalid, NotFound }
}
