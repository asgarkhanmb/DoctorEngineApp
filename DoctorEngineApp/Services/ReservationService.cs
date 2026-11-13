using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Hubs;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Reservations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class ReservationService : IReservationService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ReservationHub> _hub;
        private readonly ILogger<ReservationService> _logger;

        public ReservationService(AppDbContext context, IHubContext<ReservationHub> hub, ILogger<ReservationService> logger)
        {
            _context = context;
            _hub = hub;
            _logger = logger;
        }

        public const int OpeningHour = 10;
        public const int ClosingHour = 20;
        public const int MaxDaysAhead = 10;
        public const int SlotMinutes = 30;

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

            var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == model.ServiceId);
            if (service == null)
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
                return ReservationResult.Fail("Seçilən tarix və saat artıq rezerv olunub. Başqa saat seçin.");

            var reservation = new Reservation
            {
                FullName = model.FullName.Trim(),
                Phone = phone,
                WhatsApp = model.WhatsApp.Trim(),
                ServiceId = model.ServiceId,
                DateTime = dateTime,
                Status = "Pending",
                Note = model.Note?.Trim() ?? string.Empty,
                RejectionReason = "",
                TrackingCode = Guid.NewGuid().ToString("N")
            };

            await _context.Reservations.AddAsync(reservation);
            await _context.SaveChangesAsync();

            // Real vaxt: saat hamı üçün bağlanır, adminə bildiriş gedir
            await SafeSendAsync(_hub.Clients.All, "SlotTaken", SlotPayload(reservation));
            await SafeSendAsync(_hub.Clients.Group(ReservationHub.AdminsGroup), "ReservationCreated", AdminPayload(reservation, service.Name));

            return ReservationResult.Ok("Rezervasiyanız qəbul edildi! Statusu bu səhifədə canlı izləyə bilərsiniz.", reservation.TrackingCode);
        }

        // ================= BOŞ SAATLAR =================

        public async Task<List<ReservationSlot>> GetSlotsAsync(int serviceId, DateTime date)
        {
            var day = date.Date;
            var now = AzTime.Now;

            var busy = await _context.Reservations
                .Where(r => r.ServiceId == serviceId &&
                            r.DateTime >= day && r.DateTime < day.AddDays(1) &&
                            r.Status != "Rejected")
                .Select(r => r.DateTime)
                .ToListAsync();

            var busySet = busy.Select(d => d.ToString("HH:mm")).ToHashSet();
            var slots = new List<ReservationSlot>();

            for (var t = day.AddHours(OpeningHour); t < day.AddHours(ClosingHour); t = t.AddMinutes(SlotMinutes))
            {
                var key = t.ToString("HH:mm");
                var state = t <= now || t > now.AddDays(MaxDaysAhead) ? "past"
                          : busySet.Contains(key) ? "busy"
                          : "free";
                slots.Add(new ReservationSlot(key, state));
            }

            return slots;
        }

        // ================= MÜŞTƏRİ STATUSU =================

        public async Task<List<ReservationStatusVM>> LookupAsync(IEnumerable<string> codes)
        {
            var list = codes
                .Where(ReservationHub.IsValidCode)
                .Select(c => c.ToLowerInvariant())
                .Distinct()
                .Take(10)
                .ToList();

            if (!list.Any()) return new();

            var items = await _context.Reservations
                .Include(r => r.Service)
                .Where(r => r.TrackingCode != null && list.Contains(r.TrackingCode))
                .ToListAsync();

            return items
                .OrderByDescending(r => r.DateTime)
                .Select(ToStatus)
                .ToList();
        }

        // ================= ADMİN =================

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
            if (reservation == null) return;

            var wasActive = reservation.Status != "Rejected";
            var code = reservation.TrackingCode;

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            if (wasActive)
                await SafeSendAsync(_hub.Clients.All, "SlotFreed", SlotPayload(reservation));
            await SafeSendAsync(_hub.Clients.Group(ReservationHub.AdminsGroup), "ReservationDeleted", new { id, status = reservation.Status });
            if (code != null)
                await SafeSendAsync(_hub.Clients.Group(ReservationHub.TrackGroup(code)), "StatusChanged",
                    new { code, status = "Deleted", reason = (string?)null });
        }

        public async Task ConfirmAsync(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return;

            reservation.Status = "Confirmed";
            await _context.SaveChangesAsync();

            await NotifyStatusAsync(reservation);
        }

        public async Task RejectAsync(int id, string reason)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Service)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return;

            reservation.Status = "Rejected";
            reservation.RejectionReason = reason;
            await _context.SaveChangesAsync();

            // Rədd edilən rezervasiyanın saatı yenidən boşalır
            await SafeSendAsync(_hub.Clients.All, "SlotFreed", SlotPayload(reservation));
            await NotifyStatusAsync(reservation);
        }

        // ================= KÖMƏKÇİLƏR =================

        private async Task NotifyStatusAsync(Reservation r)
        {
            await SafeSendAsync(_hub.Clients.Group(ReservationHub.AdminsGroup), "ReservationUpdated",
                new { id = r.Id, status = r.Status, reason = r.RejectionReason });

            if (r.TrackingCode != null)
                await SafeSendAsync(_hub.Clients.Group(ReservationHub.TrackGroup(r.TrackingCode)), "StatusChanged",
                    new { code = r.TrackingCode, status = r.Status, reason = r.Status == "Rejected" ? r.RejectionReason : null });
        }

        private static object SlotPayload(Reservation r) => new
        {
            serviceId = r.ServiceId,
            date = r.DateTime.ToString("yyyy-MM-dd"),
            time = r.DateTime.ToString("HH:mm")
        };

        private static object AdminPayload(Reservation r, string serviceName) => new
        {
            id = r.Id,
            fullName = r.FullName,
            phone = r.Phone,
            whatsApp = r.WhatsApp,
            serviceName,
            date = r.DateTime.ToString("dd.MM.yyyy"),
            time = r.DateTime.ToString("HH:mm"),
            note = r.Note,
            status = r.Status
        };

        private static ReservationStatusVM ToStatus(Reservation r) => new(
            r.TrackingCode!,
            r.Service?.Name ?? "",
            r.DateTime.ToString("dd.MM.yyyy"),
            r.DateTime.ToString("HH:mm"),
            r.Status,
            r.Status == "Rejected" ? r.RejectionReason : null);

        // Bildiriş göndərilməsə də əsas əməliyyat (yaratma / təsdiq) uğurlu sayılır
        private async Task SafeSendAsync(IClientProxy clients, string method, object payload)
        {
            try { await clients.SendAsync(method, payload); }
            catch (Exception ex) { _logger.LogWarning(ex, "SignalR bildirişi göndərilmədi: {Method}", method); }
        }
    }
}
