using DoctorEngineApp.ViewModels.Maps;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IMapService
    {
        Task<bool> ExistsAsync();
        Task<MapVM?> GetSingleAsync();
        Task CreateAsync(MapVM model);
        Task UpdateAsync(MapVM model);
        Task<MapVM> GetFirstAsync();
    }
}
