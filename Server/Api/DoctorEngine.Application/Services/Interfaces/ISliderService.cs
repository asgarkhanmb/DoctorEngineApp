using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Sliders;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface ISliderService
    {
        Task<List<Slider>> GetAllAsync();
        Task<Slider?> GetByIdAsync(int id);
        Task CreateAsync(SliderVM vm);
        Task UpdateAsync(SliderVM vm);
        Task DeleteAsync(int id);
    }
}
