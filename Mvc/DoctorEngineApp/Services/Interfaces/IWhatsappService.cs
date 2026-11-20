using DoctorEngineApp.Models;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IWhatsappService
    {
        Task<WhatsappSetting> GetAsync();
        Task CreateAsync(WhatsappSetting model);
        Task UpdateAsync(WhatsappSetting model);
        Task DeleteAsync(int id);
    }
}
