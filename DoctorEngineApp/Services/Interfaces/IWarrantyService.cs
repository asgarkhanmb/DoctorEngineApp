using DoctorEngineApp.ViewModels.Warranties;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IWarrantyService
    {
        Task CreateAsync(WarrantyCreateVM vm);
        Task<List<Warranty>> GetAllAsync();
        Task<Warranty> GetAsync(int id);
    }
}
