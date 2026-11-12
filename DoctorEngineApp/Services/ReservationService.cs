using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Reservations;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class ReservationService : IReservationService
    {
        private readonly AppDbContext _context;

        public ReservationService(AppDbContext context)
        {
            _context = context;
        }

        private const int OpeningHour = 10;
        private const int ClosingHour = 20;
        private const int MaxDaysAhead = 10;

        public async Task<ReservationResult> TryCreateAsync(ReservationCreateVM model)
        {
            // Formdan gələn tarix istifadəçinin seçdiyi Bakı vaxtıdır — heç bir çevirmə edilmir,
            // müqayisə də serverin deyil, Bakının hazırkı vaxtı ilə aparılır.
            var dateTime = DateTime.SpecifyKind(model.DateTime, DateTimeKind.Unspecified);
            dateTime = dateTime.AddSeconds(-dateTime.Second).AddMilliseconds(-dateTime.Millisecond);
            var now = AzTime.Now;

            if (dateTime < now)
                return ReservationResult.Fail("Keçmiş tarix üçün rezervasiya edilə bilməz.");

            if (dateTime.Hour < OpeningHour || dateTime.Hour >= ClosingHour)
                return ReservationResult.Fail($"Rezervasiya yalnız {OpeningHour}:00 – {ClosingHour}:00 arası mümkündür.");

            if (dateTime > now.AddDays(MaxDaysAhead))
                return ReservationResult.Fail($"Rezervasiya maksimum {MaxDaysAhead} gün öncədən edilə bilər.");

            bool serviceExists = await _context.Services.AnyAsync(s => s.Id == model.ServiceId);
            if (!serviceExists)
                return ReservationResult.Fail("Seçilən xidmət tapılmadı.");

            var phone = model.Phone.Trim();
            var dayStart = dateTime.Date;
            var dayEnd = dayStart.AddDays(1);

            // 1️⃣ Eyni nömrə – eyni gün
            bool phoneExists = await _context.Reservations.AnyAsync(r =>
                r.Phone == phone &&
                r.DateTime >= dayStart && r.DateTime < dayEnd &&
                r.Status != "Rejected");

            if (phoneExists)
                return ReservationResult.Fail("Bu telefon nömrəsi ilə bu gün üçün artıq rezervasiya edilib.");

            // 2️⃣ Eyni dəqiqə – eyni xidmət
            var slotEnd = dateTime.AddMinutes(1);
            bool timeBusy = await _context.Reservations.AnyAsync(r =>
                r.ServiceId == model.ServiceId &&
                r.DateTime >= dateTime && r.DateTime < slotEnd &&
                r.Status != "Rejected");

            if (timeBusy)
                return ReservationResult.Fail("Seçilən tarix və saat artıq rezerv olunub.");

            var reservation = new Reservation
            {
                FullName = model.FullName.Trim(),
                Phone = phone,
                WhatsApp = model.WhatsApp.Trim(),
                ServiceId = model.ServiceId,
                DateTime = dateTime,
                Status = "Pending",
                Note = model.Note?.Trim() ?? string.Empty,
                RejectionReason = ""
            };

            await _context.Reservations.AddAsync(reservation);
            await _context.SaveChangesAsync();

            return ReservationResult.Ok("Rezervasiya qəbul edildi. WhatsApp üzərindən əlaqə saxlanılacaq.");
        }




        public async Task<List<ReservationAdminVM>> GetAllAsync()
        {
            return await _context.Reservations
                .Include(r => r.Service)
                .Select(r => new ReservationAdminVM
                {
                    Id = r.Id,
                    FullName = r.FullName,
                    Phone = r.Phone,
                    WhatsApp = r.WhatsApp,
                    ServiceName = r.Service.Name,
                    DateTime = r.DateTime,
                    Note = r.Note,
                    Status = r.Status,
                    RejectionReason = r.RejectionReason
                }).ToListAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation != null)
            {
                _context.Reservations.Remove(reservation);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ConfirmAsync(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation != null)
            {
                reservation.Status = "Confirmed";
                await _context.SaveChangesAsync();

                // Müştəriyə WhatsApp mesajı
                if (!string.IsNullOrEmpty(reservation.WhatsApp))
                {
                    string message =
                        $"Salam, {reservation.FullName}! Rezervasiyanız təsdiqləndi.%0A%0A" +
                        $"Xidmət: {reservation.Service.Name}%0A" +
                        $"Tarix: {reservation.DateTime:dd.MM.yyyy HH:mm}";

                    string url = $"https://wa.me/{reservation.WhatsApp}?text={message}";
                    // Admin browserdə klikləyib göndərə bilər
                }
            }
        }

        public async Task RejectAsync(int id, string reason)

        {
            var reservation = await _context.Reservations
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation != null)
            {
                reservation.Status = "Rejected";
                reservation.RejectionReason = reason;
                await _context.SaveChangesAsync();

                // Müştəriyə WhatsApp mesajı
                if (!string.IsNullOrEmpty(reservation.WhatsApp))
                {
                    string message =
                        $"Salam, {reservation.FullName}! Rezervasiyanız təəssüf ki, təsdiqlənmədi.%0A" +
                        $"Səbəb: {reason}";

                    string url = $"https://wa.me/{reservation.WhatsApp}?text={message}";
                    // Admin browserdə klikləyib göndərə bilər
                }
            }
        }

    }
}
