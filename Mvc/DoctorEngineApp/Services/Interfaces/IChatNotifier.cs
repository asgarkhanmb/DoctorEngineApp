using DoctorEngineApp.ViewModels.Chats;

namespace DoctorEngineApp.Services.Interfaces
{
    /// <summary>
    /// Çat hadisələrini canlı göndərir (SignalR): yalnız yazışmanın müştərisinə və ustalara.
    /// Sayt da, API də eyni realizasiyanı istifadə edir.
    /// </summary>
    public interface IChatNotifier
    {
        Task MessageCreatedAsync(ChatMessageDto message);
        /// <summary>Redaktə və reaksiya dəyişikliyi (mesaj bütövlükdə yenidən göndərilir).</summary>
        Task MessageChangedAsync(ChatMessageDto message);
        Task MessageDeletedAsync(string conversation, int id);
        Task ReceiptAsync(ChatReceiptDto receipt);
        /// <summary>Qrup yaradıldı / dəyişdi — üzvlərin siyahısı yenilənsin.</summary>
        Task GroupChangedAsync(string conversation);
        /// <summary>Yazışma silindi (mənim üçün və ya hamı üçün) — açıq pəncərələr təmizlənsin.</summary>
        Task ConversationClearedAsync(string conversation, string byUser, bool forEveryone);
        /// <summary>Qrup silindi — əvvəlki üzvlərə bildiriş.</summary>
        Task GroupRemovedAsync(string conversation, ChatAudience audience);
    }
}
