using DoctorEngineApp.ViewModels.Services;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IServiceService
    {
        Task<List<ServiceVM>> GetAllAsync();
        Task<ServiceVM> GetByIdAsync(int id);
        Task CreateAsync(ServiceVM model);
        Task UpdateAsync(ServiceVM model);
        Task DeleteAsync(int id);
    }
}
