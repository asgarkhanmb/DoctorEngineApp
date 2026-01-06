using DoctorEngineApp.ViewModels.Reservations;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IReservationService
    {
        Task CreateAsync(ReservationCreateVM model);
        Task<List<ReservationAdminVM>> GetAllAsync();
        Task DeleteAsync(int id);
        Task ConfirmAsync(int id);
        Task RejectAsync(int id, string reason);
    }
}
