using DoctorEngineApp.ViewModels.Services;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface ISubServiceService
    {
        Task<List<SubServiceVM>> GetAllAsync();
        Task<SubServiceEditVM?> GetForEditAsync(int id);
        Task CreateAsync(SubServiceCreateVM vm);
        Task UpdateAsync(SubServiceEditVM vm);
        Task DeleteAsync(int id);
    }
}
