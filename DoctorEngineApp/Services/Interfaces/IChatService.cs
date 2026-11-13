using DoctorEngineApp.ViewModels.Chats;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IChatService
    {
        /// <summary>Son mesajlar (köhnədən yeniyə). beforeId verilərsə ondan əvvəlkilər.</summary>
        Task<ChatPageDto> GetPageAsync(int? beforeId, int take);

        Task<ChatResult> SendAsync(ChatSendVM vm, string username, bool isAdmin);

        /// <summary>Yalnız mesajın sahibi redaktə edə bilər.</summary>
        Task<ChatResult> EditAsync(ChatEditVM vm, string username);

        /// <summary>Sahibi və ya admin silə bilər. Uğurlu olarsa true.</summary>
        Task<bool> DeleteAsync(int id, string username, bool isAdmin);

        /// <summary>Başqalarından gələn, istifadəçinin hələ oxumadığı mesajların sayı.</summary>
        Task<int> GetUnreadCountAsync(string username);

        /// <summary>Mesajları lastId-yə qədər oxunmuş kimi qeyd edir (geri getmir).</summary>
        Task MarkReadAsync(string username, int lastId);
    }
}
