using DoctorEngineApp.Models;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatMessage>> GetAllAsync();
        Task<ChatMessage> AddAsync(string message, string username);
        Task DeleteAsync(int id, string username);
        Task<ChatMessage> UpdateAsync(int id, string message, string username);
    }
}
