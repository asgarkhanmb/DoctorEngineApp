using DoctorEngineApp.ViewModels.Reservations;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IReservationService
    {
        /// <summary>
        /// Rezervasiyanı yoxlayır (tarix, iş saatları, təkrar) və uğurlu olarsa yaradır.
        /// Daxil edilən tarix Bakı vaxtı kimi qəbul olunur.
        /// </summary>
        Task<ReservationResult> TryCreateAsync(ReservationCreateVM model);
        Task<List<ReservationAdminVM>> GetAllAsync();
        Task DeleteAsync(int id);
        Task ConfirmAsync(int id);
        Task RejectAsync(int id, string reason);
    }
}
