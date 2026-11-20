using DoctorEngineApp.Localization;
using System.Security.Claims;
using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Mobil admin panel: statistika, rezervasiyalar, əlaqə mesajları, istifadəçilər.
    /// Saytın admin panelindəki eyni servisləri istifadə edir (eyni qaydalar, eyni bildirişlər).
    /// Məzmun bölmələri (slayder, xidmət, usta, ...) — AdminContentController.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [IgnoreAntiforgeryToken]   // yalnız Bearer — CSRF riski yoxdur
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IReservationService _reservations;
        private readonly IContactMessageService _messages;
        private readonly UserManager<AppUser> _users;
        private readonly RoleManager<IdentityRole> _roles;

        public AdminController(AppDbContext db, IReservationService reservations, IContactMessageService messages,
                               UserManager<AppUser> users, RoleManager<IdentityRole> roles)
        {
            _db = db;
            _reservations = reservations;
            _messages = messages;
            _users = users;
            _roles = roles;
        }

        // ================= STATİSTİKA =================

        [HttpGet("dashboard")]
        public async Task<ActionResult<AdminDashboardDto>> Dashboard()
        {
            // Saytın Dashboard səhifəsi ilə eyni hesablamalar (Bakı vaxtı)
            var today = AzTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var weekAgo = today.AddDays(-7);

            var upcoming = await _db.Reservations.AsNoTracking()
                .Where(r => r.DateTime >= today && r.Status != "Rejected")
                .OrderBy(r => r.DateTime)
                .Take(6)
                .Select(r => new AdminReservationDto(r.Id, r.FullName, r.Phone, r.WhatsApp, r.Service.Name,
                    r.DateTime, r.Note, r.Status, r.RejectionReason))
                .ToListAsync();

            return Ok(new AdminDashboardDto(
                await _db.Reservations.CountAsync(r => r.Status == "Pending"),
                await _db.Reservations.CountAsync(r => r.DateTime >= today && r.DateTime < tomorrow && r.Status != "Rejected"),
                await _db.Reservations.CountAsync(),
                await _db.ContactMessages.CountAsync(m => m.CreatedDate >= weekAgo),
                await _db.ContactMessages.CountAsync(),
                await _db.ChatMessages.CountAsync(m => m.CreatedDate >= today),
                await _db.Users.CountAsync(),
                await _db.Warranties.CountAsync(),
                upcoming));
        }

        // ================= REZERVASİYALAR =================

        [HttpGet("reservations")]
        public async Task<ActionResult<IReadOnlyList<AdminReservationDto>>> Reservations([FromQuery] string? status = null)
        {
            var list = await _reservations.GetAllAsync();
            return Ok(list
                .Where(r => string.IsNullOrEmpty(status) || r.Status == status)
                .Select(r => new AdminReservationDto(r.Id, r.FullName ?? "", r.Phone ?? "", r.WhatsApp, r.ServiceName ?? "",
                    r.DateTime, r.Note, r.Status ?? "", r.RejectionReason))
                .ToList());
        }

        [HttpPost("reservations/{id:int}/confirm")]
        public async Task<IActionResult> Confirm(int id)
        {
            await _reservations.ConfirmAsync(id);
            return NoContent();
        }

        [HttpPost("reservations/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectReservationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) return Error(400, L.T("İmtina səbəbini yazın."));
            await _reservations.RejectAsync(id, request.Reason.Trim());
            return NoContent();
        }

        [HttpDelete("reservations/{id:int}")]
        public async Task<IActionResult> DeleteReservation(int id)
        {
            await _reservations.DeleteAsync(id);
            return NoContent();
        }

        // ================= ƏLAQƏ MESAJLARI =================

        [HttpGet("messages")]
        public async Task<ActionResult<IReadOnlyList<AdminMessageDto>>> Messages()
        {
            var list = await _messages.GetAllAsync();
            return Ok(list.OrderByDescending(m => m.CreatedDate)
                .Select(m => new AdminMessageDto(m.Id, m.FullName ?? "", m.Email ?? "", m.Subject ?? "", m.Message ?? "", m.CreatedDate))
                .ToList());
        }

        /// <summary>Tək mesaj (saytdakı Admin/ContactMessage/Detail).</summary>
        [HttpGet("messages/{id:int}")]
        public async Task<ActionResult<AdminMessageDto>> Message(int id)
        {
            var m = await _messages.GetByIdAsync(id);
            if (m == null) return Error(404, L.T("Qeyd tapılmadı."));
            return Ok(new AdminMessageDto(m.Id, m.FullName ?? "", m.Email ?? "", m.Subject ?? "", m.Message ?? "", m.CreatedDate));
        }

        [HttpDelete("messages/{id:int}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            await _messages.DeleteAsync(id);
            return NoContent();
        }

        // ================= VİDEO KÖÇÜRMƏ (saytdakı Admin/SubService/Move) =================

        /// <summary>Videonu videosu olmayan kateqoriyaya köçürür (hər kateqoriyada bir video qaydası).</summary>
        [HttpPost("videos/{id:int}/move")]
        public async Task<ActionResult<MessageResponse>> MoveVideo(int id, [FromBody] MoveVideoRequest request)
        {
            var entity = await _db.SubServices.FindAsync(id);
            if (entity == null) return Error(404, L.T("Qeyd tapılmadı."));

            var target = await _db.ServiceCategories.Include(c => c.SubServices)
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId);
            if (target == null) return Error(404, "Kateqoriya tapılmadı.");
            if (target.SubServices.Any()) return Error(400, $"“{target.Name}” kateqoriyasının artıq videosu var.");

            entity.CategoryId = target.Id;
            await _db.SaveChangesAsync();
            return Ok(new MessageResponse($"Video “{target.Name}” kateqoriyasına köçürüldü."));
        }

        // ================= ZƏMANƏTLƏR (saytdakı Admin/Warranty/Detail və Print) =================

        [HttpGet("warranties/{id:int}")]
        public async Task<ActionResult<AdminWarrantyDto>> Warranty(int id)
        {
            var w = await _db.Warranties.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
            if (w == null) return Error(404, L.T("Qeyd tapılmadı."));
            return Ok(new AdminWarrantyDto(w.Id, w.CustomerFullName, w.VehicleModel, w.VehicleYear, w.Terms, w.CreatedDate,
                (w.Items ?? new()).Select(i => new AdminWarrantyItemDto(i.PartName, i.WarrantyMonth)).ToList()));
        }

        /// <summary>Zəmanət sənədi PDF (A4) — saytdakı ilə eyni şablon (Views/Warranty/Print.cshtml).</summary>
        [HttpGet("warranties/{id:int}/print")]
        [Produces("application/pdf")]
        public async Task<IActionResult> PrintWarranty(int id)
        {
            var w = await _db.Warranties.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
            if (w == null) return Error(404, L.T("Qeyd tapılmadı."));

            return new Rotativa.AspNetCore.ViewAsPdf("~/Views/Warranty/Print.cshtml", w)
            {
                FileName = $"zemanet_{id}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(10, 10, 10, 10)
            };
        }

        // ================= İSTİFADƏÇİLƏR =================

        [HttpGet("users")]
        public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> Users([FromQuery] string? q = null, [FromQuery] int take = 300)
        {
            var query = _users.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim();
                query = query.Where(u => u.UserName!.Contains(s) || u.Email!.Contains(s) || u.FullName.Contains(s));
            }
            var users = await query.OrderBy(u => u.UserName).Take(Math.Clamp(take, 1, 10_000)).ToListAsync();   // sayt hamısını göstərir (take=10000)
            var now = DateTimeOffset.UtcNow;

            var list = new List<AdminUserDto>();
            foreach (var u in users)
            {
                list.Add(new AdminUserDto(
                    u.Id, u.UserName ?? "", u.FullName ?? "", u.Email ?? "",
                    string.IsNullOrEmpty(u.ProfileImage) ? null : "/uploads/avatars/" + u.ProfileImage,
                    u.EmailConfirmed,
                    u.LockoutEnd.HasValue && u.LockoutEnd > now,
                    (await _users.GetRolesAsync(u)).ToList()));
            }
            return Ok(list);
        }

        [HttpGet("roles")]
        public async Task<ActionResult<IReadOnlyList<string>>> Roles() =>
            Ok(await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync());

        [HttpPost("users/{id}/roles")]
        public async Task<IActionResult> AddRole(string id, [FromBody] RoleRequest request)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return Error(404, L.T("İstifadəçi tapılmadı."));
            if (string.IsNullOrWhiteSpace(request.Role) || !await _roles.RoleExistsAsync(request.Role))
                return Error(400, L.T("Rol tapılmadı."));
            if (await _users.IsInRoleAsync(user, request.Role))
                return Error(400, L.T("{0} artıq \"{1}\" roluna malikdir.", user.UserName, request.Role));

            await _users.AddToRoleAsync(user, request.Role);
            return NoContent();
        }

        [HttpDelete("users/{id}/roles/{role}")]
        public async Task<IActionResult> RemoveRole(string id, string role)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return Error(404, L.T("İstifadəçi tapılmadı."));

            // Saytdakı kimi: admin özünü admin rolundan çıxara bilməz (paneldən kənarda qalmasın)
            if (role == "Admin" && user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Error(400, L.T("Öz admin rolunuzu silə bilməzsiniz."));

            await _users.RemoveFromRoleAsync(user, role);
            return NoContent();
        }

        private ObjectResult Error(int code, string message) => StatusCode(code, new ApiError(message, code));
    }
}
