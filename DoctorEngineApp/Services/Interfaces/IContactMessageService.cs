using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.ContactMessage;

public interface IContactMessageService
{ 
    Task CreateAsync(ContactMessageCreateVM model);
    Task<List<ContactMessage>> GetAllAsync();
    Task<ContactMessage?> GetByIdAsync(int id);
    Task DeleteAsync(int id);
}
