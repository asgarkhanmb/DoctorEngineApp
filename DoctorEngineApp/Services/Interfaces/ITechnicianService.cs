using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Technicians;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface ITechnicianService
    {

        Task<List<Technician>> GetAllAsync();
        Task<Technician> GetByIdAsync(int id);
        Task CreateAsync(TechnicianCreateVM vm);
        Task EditAsync(TechnicianEditVM vm);
        Task DeleteAsync(int id);
    }
}
