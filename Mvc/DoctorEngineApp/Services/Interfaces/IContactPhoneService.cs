using DoctorEngineApp.ViewModels.ContactPhones;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IContactPhoneService
    {
        Task<List<ContactPhoneVM>> GetAllAsync();
        Task<ContactPhoneVM?> GetByIdAsync(int id);
        Task CreateAsync(ContactPhoneVM model);
        Task UpdateAsync(ContactPhoneVM model);
        Task DeleteAsync(int id);
    }
}
