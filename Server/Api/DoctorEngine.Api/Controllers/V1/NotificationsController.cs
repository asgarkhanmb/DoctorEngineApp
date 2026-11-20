using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Localization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Tətbiq daxili bildirişlər (Instagram-dakı "ürək" bölməsi): kim videonuzu bəyəndi, rəy yazdı, rəyinizə cavab verdi.
    /// Hər istifadəçi yalnız öz bildirişlərini görür, oxundu edir və silir.
    /// </summary>
    [ApiController]
    [Route("api/v1/notifications")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [IgnoreAntiforgeryToken]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public NotificationsController(AppDbContext db) => _db = db;

        private string Me => User.Identity!.Name!;

        /// <summary>Ən yenidən köhnəyə. Növbəti səhifə: ?before={son Id}.</summary>
        [HttpGet]
        public async Task<ActionResult<NotificationPageDto>> List([FromQuery] int? before, [FromQuery] int take = 30)
        {
            take = Math.Clamp(take, 1, 100);
            var me = Me;
            var q = _db.Notifications.AsNoTracking().Where(n => n.UserName == me);
            if (before is > 0) q = q.Where(n => n.Id < before);
            var rows = await q.OrderByDescending(n => n.Id).Take(take + 1).ToListAsync();
            var hasMore = rows.Count > take;
            if (hasMore) rows.RemoveAt(rows.Count - 1);

            var actors = rows.Select(n => n.ActorUserName).Distinct().ToList();
            var users = (await _db.Users.AsNoTracking().Where(u => actors.Contains(u.UserName!))
                    .Select(u => new { u.UserName, u.FullName, u.ProfileImage }).ToListAsync())
                .ToDictionary(u => u.UserName!, StringComparer.OrdinalIgnoreCase);
            var unread = await _db.Notifications.CountAsync(n => n.UserName == me && !n.IsRead);

            return Ok(new NotificationPageDto(rows.Select(n =>
            {
                users.TryGetValue(n.ActorUserName, out var u);
                return new NotificationDto(n.Id, n.Type, n.ActorUserName,
                    string.IsNullOrWhiteSpace(u?.FullName) ? n.ActorUserName : u!.FullName,
                    string.IsNullOrEmpty(u?.ProfileImage) ? null : "/uploads/avatars/" + u!.ProfileImage,
                    n.ReelId, n.CommentId, n.Snippet, n.IsRead, n.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ss"));
            }).ToList(), hasMore, unread));
        }

        /// <summary>Oxunmamışların sayı — alt menyudakı nişan üçün (tez-tez sorğulanır).</summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<UnreadCountDto>> UnreadCount()
        {
            var me = Me;
            return Ok(new UnreadCountDto(await _db.Notifications.CountAsync(n => n.UserName == me && !n.IsRead)));
        }

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> Read(int id, [FromQuery] bool read = true)
        {
            var me = Me;
            var n = await _db.Notifications.Where(x => x.Id == id && x.UserName == me)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, read));
            return n == 0 ? NotFound(new ApiError(L.T("Bildiriş tapılmadı."), 404)) : NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> ReadAll()
        {
            var me = Me;
            await _db.Notifications.Where(x => x.UserName == me && !x.IsRead).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var me = Me;
            var n = await _db.Notifications.Where(x => x.Id == id && x.UserName == me).ExecuteDeleteAsync();
            return n == 0 ? NotFound(new ApiError(L.T("Bildiriş tapılmadı."), 404)) : NoContent();
        }

        /// <summary>Hamısını sil (yalnız özününküləri).</summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteAll()
        {
            var me = Me;
            await _db.Notifications.Where(x => x.UserName == me).ExecuteDeleteAsync();
            return NoContent();
        }
    }
}
