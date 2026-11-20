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

        /// <summary>Xidmət və gün üçün bütün saatlar (30 dəq.) və onların vəziyyəti.</summary>
        Task<List<ReservationSlot>> GetSlotsAsync(int serviceId, DateTime date);

        /// <summary>İzləmə kodlarına görə müştəri statusları (şəxsi məlumat olmadan).</summary>
        Task<List<ReservationStatusVM>> LookupAsync(IEnumerable<string> codes);
        Task DeleteAsync(int id);
        Task ConfirmAsync(int id);
        Task RejectAsync(int id, string reason);
    }
}
