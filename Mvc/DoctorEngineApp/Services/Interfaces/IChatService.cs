using DoctorEngineApp.ViewModels.Chats;

namespace DoctorEngineApp.Services.Interfaces
{
    /// <summary>Hadisənin kimə gedəcəyi: hamıya (bütün istifadəçilər qrupu) və ya adları sadalanan istifadəçilərə (+ ustalar).</summary>
    /// <param name="Private">Şəxsi yazışma ("d:") — yalnız üzvlərə, ustalara yox.</param>
    public record ChatAudience(bool IsGroup, bool Everyone, IReadOnlyList<string> UserNames, bool Private = false);

    /// <summary>
    /// Çat (WhatsApp kimi):
    ///  - Şəxsi yazışma: müştəri ↔ ustalar (açar = müştərinin adı)
    ///  - Qrup: ustalar yaradır, üzvlər bir-birinin mesajlarını görür (açar = "g:{id}")
    ///  - Yayım: ustanın mesajı hər müştərinin şəxsi yazışmasına ayrıca gedir
    /// </summary>
    public interface IChatService
    {
        /// <summary>Yazışmanın açarı (icazə yoxlanılır). Etibarsızdırsa və ya icazə yoxdursa null.</summary>
        Task<string?> ResolveConversationAsync(string username, bool isAdmin, string? conversation);

        /// <summary>Son mesajlar (köhnədən yeniyə) + qarşı tərəf / qrup məlumatı.</summary>
        Task<ChatPageDto> GetPageAsync(string conversation, string viewer, bool viewerIsAdmin, int? beforeId, int take);

        /// <summary>"Çatlar" siyahısı: usta — bütün şəxsi yazışmalar + qruplar; müştəri — ustalarla yazışması + qrupları.</summary>
        Task<List<ChatConversationDto>> GetConversationsAsync(string viewer, bool isAdmin);

        /// <summary>Ustalar üçün şəxsi yazışmanın siyahı sətri (canlı yeniləmə).</summary>
        Task<ChatConversationDto?> GetConversationAsync(string conversation);

        /// <summary>Hadisəni kimlər almalıdır.</summary>
        Task<ChatAudience> GetAudienceAsync(string conversation);

        Task<List<ChatMessageDto>> SearchAsync(string conversation, string viewer, string query);

        Task<ChatResult> SendAsync(ChatSendVM vm, string conversation, string username, bool isAdmin);
        Task<ChatResult> EditAsync(ChatEditVM vm, string username);
        /// <summary>Sahibi və ya usta silə bilər. Uğurlu olarsa yazışmanın açarı, yoxsa null.</summary>
        Task<string?> DeleteAsync(int id, string username, bool isAdmin);
        Task<ChatMessageDto?> ReactAsync(int id, string? emoji, string username, bool isAdmin);

        Task<int> GetUnreadCountAsync(string username, bool isAdmin);
        Task<ChatReceiptDto?> MarkReadAsync(string conversation, string reader, bool readerIsAdmin, int lastId);
        Task<ChatReceiptDto?> MarkDeliveredAsync(string conversation, string reader, bool readerIsAdmin, int lastId);

        /// <summary>
        /// İstifadəçi qoşulanda (sayt / tətbiq açılanda): ona gələn bütün mesajlar "çatdı" (✓✓) olur — WhatsApp kimi.
        /// Dəyişən yazışmaların ✓✓ vəziyyəti qaytarılır (qarşı tərəfə canlı göndərmək üçün).
        /// </summary>
        Task<List<ChatReceiptDto>> MarkAllDeliveredAsync(string username, bool isAdmin);

        Task SetPresenceAsync(string username, bool online);
        Task<(bool Online, string? LastSeen)> GetSupportPresenceAsync();

        // ---------- Qruplar (ustalar) ----------
        Task<List<ChatUserDto>> GetUsersAsync(string? query);
        Task<(ChatGroupInfoDto? Group, string? Error)> CreateGroupAsync(ChatGroupSaveVM vm, string creator);
        Task<(ChatGroupInfoDto? Group, string? Error)> UpdateGroupAsync(int id, ChatGroupSaveVM vm);
        Task<ChatGroupInfoDto?> GetGroupAsync(int id, string viewer, bool isAdmin);
        /// <summary>Qrupu və bütün mesajlarını silir; əvvəlki üzvlər (bildiriş üçün) qaytarılır.</summary>
        Task<ChatAudience?> DeleteGroupAsync(int id);
        Task<ChatGroupInfoDto?> RemoveMemberAsync(int id, string userName);

        // ---------- Profil və media (WhatsApp kimi) ----------
        Task<ChatProfileDto?> GetProfileAsync(string username);
        Task<(ChatProfileDto? Profile, string? Error)> UpdateProfileAsync(string username, ChatProfileSaveVM vm);
        /// <summary>Profil şəklini dəyiş (file = null — sil).</summary>
        Task<(ChatProfileDto? Profile, string? Error)> SetAvatarAsync(string username, IFormFile? file);
        /// <summary>Başlığa toxunanda: əlaqənin / qrupun məlumatı və media sayları.</summary>
        Task<ChatContactDto?> GetContactAsync(string conversation, string viewer, bool viewerIsAdmin);
        /// <summary>Yazışmanın mediası: kind = "media" (şəkil, video), "audio", "links". Yenidən köhnəyə.</summary>
        Task<ChatPageDto> GetMediaAsync(string conversation, string viewer, string kind, int? beforeId, int take);

        /// <summary>
        /// "Yazışmanı sil": forEveryone = false — yalnız mənim üçün (mesajlar mənə görünmür);
        /// forEveryone = true — yalnız usta: bütün mesajlar və fayllar hamı üçün silinir.
        /// </summary>
        Task<(bool Ok, string? Error)> ClearConversationAsync(string conversation, string username, bool isAdmin, bool forEveryone);

        // ---------- Yeni çat ("+") ----------
        /// <summary>Yeni çat üçün istifadəçilər (hər kəs görür; email göstərilmir, özüm daxil deyiləm).</summary>
        Task<List<ChatUserDto>> GetPeopleAsync(string viewer, string? query);
        /// <summary>
        /// İstifadəçiyə yaz: yazışmanın açarı. Müştəri ↔ usta — mövcud "ustalarla yazışma";
        /// müştəri ↔ müştəri və usta ↔ usta — şəxsi yazışma ("d:{id}", lazım olsa yaradılır).
        /// </summary>
        Task<(string? Conversation, string? Error)> StartDirectAsync(string me, bool meIsAdmin, string userName);

        // ---------- Yayım ----------
        Task<(List<ChatMessageDto> Messages, string? Error)> BroadcastAsync(ChatBroadcastVM vm, string admin);
    }
}
