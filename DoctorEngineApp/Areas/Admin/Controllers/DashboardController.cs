using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var now = AzTime.Now;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var weekAgo = today.AddDays(-7);

            var vm = new DashboardVM
            {
                PendingReservations = await _db.Reservations.CountAsync(r => r.Status == "Pending"),
                TodayReservations = await _db.Reservations.CountAsync(r => r.DateTime >= today && r.DateTime < tomorrow && r.Status != "Rejected"),
                TotalReservations = await _db.Reservations.CountAsync(),
                MessagesLast7Days = await _db.ContactMessages.CountAsync(m => m.CreatedDate >= weekAgo),
                TotalMessages = await _db.ContactMessages.CountAsync(),
                ChatMessagesToday = await _db.ChatMessages.CountAsync(m => m.CreatedDate >= today),
                Users = await _db.Users.CountAsync(),
                Warranties = await _db.Warranties.CountAsync(),

                // Bu gündən sonrakı, rədd edilməmiş rezervasiyalar (ən yaxın birinci)
                UpcomingReservations = await _db.Reservations
                    .AsNoTracking()
                    .Where(r => r.DateTime >= today && r.Status != "Rejected")
                    .OrderBy(r => r.DateTime)
                    .Take(6)
                    .Select(r => new DashboardReservationVM
                    {
                        Id = r.Id,
                        FullName = r.FullName,
                        Phone = r.Phone,
                        WhatsApp = r.WhatsApp,
                        ServiceName = r.Service.Name,
                        DateTime = r.DateTime,
                        Status = r.Status
                    })
                    .ToListAsync(),

                LatestMessages = await _db.ContactMessages
                    .AsNoTracking()
                    .OrderByDescending(m => m.CreatedDate)
                    .Take(5)
                    .ToListAsync(),

                LatestChat = await _db.ChatMessages
                    .AsNoTracking()
                    .Where(m => !m.SoftDeleted)
                    .OrderByDescending(m => m.Id)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}
