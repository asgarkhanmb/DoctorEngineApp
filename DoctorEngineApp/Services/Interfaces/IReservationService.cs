using DoctorEngineApp.ViewModels.Reservations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IReservationService
    {
        Task CreateAsync(ReservationCreateVM model);
        Task<List<ReservationAdminVM>> GetAllAsync();
        Task DeleteAsync(int id);
        Task ConfirmAsync(int id);
        Task RejectAsync(int id, string reason);
        Task<bool> ExistsAsync(string phone, int serviceId, DateTime dateTime);


    }
}
