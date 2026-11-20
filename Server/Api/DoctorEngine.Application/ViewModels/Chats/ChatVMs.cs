using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Chats
{
    /// <summary>Yeni mesaj (multipart/form-data): mətn və/və ya bir şəkil/video/səs.</summary>
    public class ChatSendVM
    {
        [StringLength(1000, ErrorMessage = "Mesaj maksimum 1000 simvol ola bilər.")]
        public string? Message { get; set; }

        public IFormFile? File { get; set; }

        /// <summary>Cavab verilən mesaj (istəyə bağlı).</summary>
        public int? ReplyToId { get; set; }

        /// <summary>Yalnız usta üçün: hansı müştərinin yazışmasına yazır. Müştəri üçün nəzərə alınmır.</summary>
        [StringLength(256)]
        public string? Conversation { get; set; }
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

    /// <summary>Cavab verilən mesajın qısa görünüşü (balonun içində sitat).</summary>
    public class ChatReplyDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        /// <summary>Mətn (ən çox 160 simvol); silinibsə null.</summary>
        public string? Message { get; set; }
        public string? AttachmentType { get; set; }
        public bool Deleted { get; set; }
    }

    /// <summary>Bir emoji və onu qoyan istifadəçilər.</summary>
    public class ChatReactionDto
    {
        public string Emoji { get; set; } = string.Empty;
        public List<string> Users { get; set; } = new();
    }

    /// <summary>Brauzerə, tətbiqə və SignalR-a göndərilən mesaj.</summary>
    public class ChatMessageDto
    {
        public int Id { get; set; }
        /// <summary>Yazışma (müştərinin istifadəçi adı).</summary>
        public string Conversation { get; set; } = string.Empty;
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
        public ChatReplyDto? ReplyTo { get; set; }
        public List<ChatReactionDto> Reactions { get; set; } = new();
    }

    /// <summary>
    /// Qarşı tərəf: müştəri üçün — ustalar (DoctorEngine), usta üçün — müştəri.
    /// ReadUpToId / DeliveredUpToId: mənim mesajlarımdan hansıları qarşı tərəf alıb / oxuyub (✓✓ / mavi ✓✓).
    /// </summary>
    public class ChatPeerDto
    {
        public string Conversation { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool Online { get; set; }
        /// <summary>Son görülmə (Bakı vaxtı, ISO "yyyy-MM-ddTHH:mm:ss"); onlayndırsa və ya bilinmirsə null.</summary>
        public string? LastSeen { get; set; }
        public int ReadUpToId { get; set; }
        public int DeliveredUpToId { get; set; }
        /// <summary>Qrup yazışmasıdır ("g:{id}").</summary>
        public bool IsGroup { get; set; }
        /// <summary>İki istifadəçinin şəxsi yazışması ("d:{id}").</summary>
        public bool IsDirect { get; set; }
        /// <summary>Şəxsi yazışmada qarşı tərəfin istifadəçi adı.</summary>
        public string? PeerUserName { get; set; }
        public int MemberCount { get; set; }
        /// <summary>Mən bu yazışmaya yaza bilərəmmi (qrupda "yalnız ustalar" ola bilər).</summary>
        public bool CanWrite { get; set; } = true;
        public bool AdminsOnly { get; set; }
        public bool AllUsers { get; set; }
    }

    public class ChatPageDto
    {
        public List<ChatMessageDto> Messages { get; set; } = new();
        public bool HasMore { get; set; }
        public ChatPeerDto? Peer { get; set; }
    }

    /// <summary>Ustanın yazışmalar siyahısında bir sətir (WhatsApp-ın "Çatlar" siyahısı kimi).</summary>
    public class ChatConversationDto
    {
        public string Conversation { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool Online { get; set; }
        public string? LastSeen { get; set; }
        public int LastMessageId { get; set; }
        /// <summary>Son mesajın qısa mətni ("📷 Şəkil" və s.).</summary>
        public string? LastMessage { get; set; }
        public bool LastFromAdmin { get; set; }
        /// <summary>Bakı vaxtı, ISO "yyyy-MM-ddTHH:mm:ss"</summary>
        public string LastDate { get; set; } = string.Empty;
        public int Unread { get; set; }
        /// <summary>Son mesaj ustanındırsa: müştəri onu alıb / oxuyub (✓✓).</summary>
        public bool LastDelivered { get; set; }
        public bool LastRead { get; set; }
        public bool IsGroup { get; set; }
        /// <summary>İki istifadəçinin şəxsi yazışması ("d:{id}").</summary>
        public bool IsDirect { get; set; }
        /// <summary>Şəxsi yazışmada qarşı tərəfin istifadəçi adı.</summary>
        public string? PeerUserName { get; set; }
        /// <summary>Qrupda son mesajı yazanın adı ("Siz", "Ali").</summary>
        public string? LastSender { get; set; }
        /// <summary>Müştəri üçün: ustalarla şəxsi yazışma sətri.</summary>
        public bool IsSupport { get; set; }
    }

    /// <summary>Qrupun məlumatı və üzvləri.</summary>
    public class ChatGroupInfoDto
    {
        public string Conversation { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool AllUsers { get; set; }
        public bool AdminsOnly { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedDate { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public List<ChatUserDto> Members { get; set; } = new();
    }

    /// <summary>İstifadəçi (qrupa üzv seçmək üçün).</summary>
    public class ChatUserDto
    {
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsAdmin { get; set; }
        public bool Online { get; set; }
    }

    /// <summary>Qrup yarat / dəyiş (form). Members — təkrarlanan sahə.</summary>
    public class ChatGroupSaveVM
    {
        [Required(ErrorMessage = "Qrupun adını yazın.")]
        [StringLength(100, ErrorMessage = "Qrupun adı maksimum 100 simvol ola bilər.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        public bool AllUsers { get; set; }
        public bool AdminsOnly { get; set; }
        public List<string>? Members { get; set; }
    }

    /// <summary>Yayım: mesaj hər istifadəçinin şəxsi yazışmasına ayrıca göndərilir.</summary>
    public class ChatBroadcastVM
    {
        [StringLength(1000, ErrorMessage = "Mesaj maksimum 1000 simvol ola bilər.")]
        public string? Message { get; set; }

        public IFormFile? File { get; set; }
    }

    /// <summary>✓✓ dəyişikliyi: Side = "admin" (ustalar aldı/oxudu) və ya "user" (müştəri aldı/oxudu).</summary>
    public class ChatReceiptDto
    {
        public string Conversation { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public int DeliveredUpToId { get; set; }
        public int ReadUpToId { get; set; }
    }

    /// <summary>Mənim profilim (çatda: ad, şəkil, "Haqqında").</summary>
    public class ChatProfileDto
    {
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
        public string? About { get; set; }
    }

    /// <summary>Profil dəyiş (form).</summary>
    public class ChatProfileSaveVM
    {
        [Required(ErrorMessage = "Adınızı yazın.")]
        [StringLength(100, ErrorMessage = "Ad maksimum 100 simvol ola bilər.")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(140, ErrorMessage = "\"Haqqında\" maksimum 140 simvol ola bilər.")]
        public string? About { get; set; }
    }

    /// <summary>Əlaqə / qrup məlumatı (WhatsApp-da başlığa toxunanda açılan səhifə).</summary>
    public class ChatContactDto
    {
        public string Conversation { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? About { get; set; }
        public bool Online { get; set; }
        public string? LastSeen { get; set; }
        public bool IsGroup { get; set; }
        /// <summary>İki istifadəçinin şəxsi yazışması ("d:{id}").</summary>
        public bool IsDirect { get; set; }
        /// <summary>Şəxsi yazışmada qarşı tərəfin istifadəçi adı.</summary>
        public string? PeerUserName { get; set; }
        public bool IsSupport { get; set; }
        /// <summary>Yalnız ustalara: müştərinin emaili və telefonu.</summary>
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int MediaCount { get; set; }
        public int AudioCount { get; set; }
        public int LinkCount { get; set; }
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
