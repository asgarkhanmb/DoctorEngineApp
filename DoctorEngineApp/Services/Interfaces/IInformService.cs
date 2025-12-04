using DoctorEngineApp.Models;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IInformService
    {
        Task<List<Inform>> GetAllAsync();
        Task<Inform?> GetByIdAsync(int id);
        Task CreateAsync(Inform inform);
        Task UpdateAsync(Inform inform);
        Task DeleteAsync(int id);
    }
}
