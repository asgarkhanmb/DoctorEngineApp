using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Warranties;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IWarrantyService
    {
        Task<List<Warranty>> GetAllAsync();
        Task<Warranty> GetByIdAsync(int id);
        Task<Warranty> CreateAsync(WarrantyCreateVM model);
        Task DeleteAsync(int id);
    }
}
