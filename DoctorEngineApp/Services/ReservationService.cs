using DoctorEngineApp.Data;
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

        public async Task CreateAsync(ReservationCreateVM model)
        {
            // 1️⃣ Eyni nömrə – eyni gün
            bool phoneExists = await _context.Reservations.AnyAsync(r =>
                r.Phone == model.Phone &&
                r.DateTime.Date == model.DateTime.Date &&
                r.Status != "Rejected");

            if (phoneExists)
                throw new InvalidOperationException(
                    "Bu telefon nömrəsi ilə bu gün üçün artıq rezervasiya edilib.");

            // 2️⃣ Eyni saat – eyni xidmət
            bool timeBusy = await _context.Reservations.AnyAsync(r =>
                r.ServiceId == model.ServiceId &&
                r.DateTime.Year == model.DateTime.Year &&
                r.DateTime.Month == model.DateTime.Month &&
                r.DateTime.Day == model.DateTime.Day &&
                r.DateTime.Hour == model.DateTime.Hour &&
                r.DateTime.Minute == model.DateTime.Minute &&
                r.Status != "Rejected");

            if (timeBusy)
                throw new InvalidOperationException(
                    "Seçilən tarix və saat artıq rezerv olunub.");

            Reservation reservation = new Reservation
            {
                FullName = model.FullName,
                Phone = model.Phone,
                WhatsApp = model.WhatsApp,
                ServiceId = model.ServiceId,
                DateTime = model.DateTime,
                Status = "Pending",
                Note = model.Note,
                RejectionReason = ""
            };

            await _context.Reservations.AddAsync(reservation);
            await _context.SaveChangesAsync();
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
                    ServiceWhatsApp = r.Service.WhatsApp,
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
        public async Task<bool> ExistsAsync(string phone, int serviceId, DateTime dateTime)
        {
            return await _context.Reservations.AnyAsync(r =>
                r.Phone == phone &&
                r.ServiceId == serviceId &&
                r.DateTime == dateTime
            );
        }

    }
}
