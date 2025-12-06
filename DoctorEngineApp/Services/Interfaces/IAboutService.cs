using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Abouts;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IAboutService 
    {
        Task<List<About>> GetAllAsync();
        Task<About?> GetByIdAsync(int id);
        Task<About?> GetSingleAsync(); // if you store only one About
        Task CreateAsync(AboutCreateVM vm);
        Task UpdateAsync(AboutEditVM vm);
        Task DeleteAsync(int id);
    }
}
