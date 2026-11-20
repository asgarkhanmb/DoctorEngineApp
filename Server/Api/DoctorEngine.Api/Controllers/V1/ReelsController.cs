using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Localization;
using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// "Videolar" lenti — Instagram Reels kimi. Admin video paylaşır / silir; hamı baxır,
    /// daxil olmuş istifadəçilər bəyənir və rəy yazır. Rəyi müəllifi və ya admin silə bilər.
    /// </summary>
    [ApiController]
    [Route("api/v1/reels")]
    [Produces("application/json")]
    [IgnoreAntiforgeryToken]   // yalnız Bearer
    public class ReelsController : ControllerBase
    {
        private const long MaxVideoBytes = 500L * 1024 * 1024;   // 500 MB
        private static readonly string[] VideoExtensions = [".mp4", ".mov", ".m4v", ".webm"];

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ReelsController> _logger;
        private HashSet<int> _likedComments = [];   // Comments() doldurur — ToDto "Liked" üçün

        public ReelsController(AppDbContext db, IWebHostEnvironment env, ILogger<ReelsController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        private string? Me => User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        private bool IsAdmin => User.IsInRole("Admin");

        // ================= LENT =================

        /// <summary>Ən yenidən köhnəyə. Növbəti səhifə: ?before={son Id}.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ReelPageDto>> Feed([FromQuery] int? before, [FromQuery] int take = 10)
        {
            take = Math.Clamp(take, 1, 30);
            var q = _db.Reels.AsNoTracking().Where(r => !r.SoftDeleted);
            if (before is > 0) q = q.Where(r => r.Id < before);
            var rows = await q.OrderByDescending(r => r.Id).Take(take + 1).ToListAsync();
            var hasMore = rows.Count > take;
            if (hasMore) rows.RemoveAt(rows.Count - 1);
            return Ok(new ReelPageDto(await ToDtosAsync(rows), hasMore));
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<ReelDto>> Get(int id)
        {
            var reel = await _db.Reels.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && !r.SoftDeleted);
            if (reel == null) return Error(404, L.T("Video tapılmadı."));
            return Ok((await ToDtosAsync([reel]))[0]);
        }

        /// <summary>Baxış sayı (tətbiq videonu bir neçə saniyə izləyəndə bir dəfə göndərir).</summary>
        [HttpPost("{id:int}/view")]
        [AllowAnonymous]
        public async Task<IActionResult> AddView(int id)
        {
            await _db.Reels.Where(r => r.Id == id && !r.SoftDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ViewCount, r => r.ViewCount + 1));
            return NoContent();
        }

        // ================= ADMIN: PAYLAŞ / REDAKTƏ / SİL =================

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [RequestSizeLimit(MaxVideoBytes + 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxVideoBytes + 1024 * 1024)]
        public async Task<ActionResult<ReelDto>> Create([FromForm] IFormFile? file, [FromForm] string? caption)
        {
            if (file == null || file.Length == 0) return Error(400, L.T("Video faylı seçin."));
            if (file.Length > MaxVideoBytes) return Error(400, L.T("Video 500 MB-dan böyük ola bilməz."));

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!VideoExtensions.Contains(ext) || !FileSignature.Matches(file, ext))
                return Error(400, L.T("Yalnız video faylı (mp4, mov, webm) yükləmək olar."));

            var folder = Path.Combine(_env.WebRootPath, "uploads", "reels");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid().ToString("N") + ext;
            await using (var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew))
                await file.CopyToAsync(stream);

            var reel = new Reel
            {
                VideoPath = name,
                Caption = Clean(caption, 2200),
                AuthorUserName = Me!,
                CreatedDate = AzTime.Now
            };
            _db.Reels.Add(reel);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Reel {Id} paylaşıldı ({User}, {Bytes} bayt)", reel.Id, Me, file.Length);
            return Ok((await ToDtosAsync([reel]))[0]);
        }

        [HttpPut("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<ActionResult<ReelDto>> Update(int id, [FromBody] ReelUpdateRequest request)
        {
            var reel = await _db.Reels.FirstOrDefaultAsync(r => r.Id == id && !r.SoftDeleted);
            if (reel == null) return Error(404, L.T("Video tapılmadı."));
            reel.Caption = Clean(request.Caption, 2200);
            await _db.SaveChangesAsync();
            return Ok((await ToDtosAsync([reel]))[0]);
        }

        [HttpDelete("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var reel = await _db.Reels.FirstOrDefaultAsync(r => r.Id == id && !r.SoftDeleted);
            if (reel == null) return Error(404, L.T("Video tapılmadı."));

            reel.SoftDeleted = true;
            await _db.SaveChangesAsync();
            await _db.ReelLikes.Where(l => l.ReelId == id).ExecuteDeleteAsync();
            await _db.ReelCommentLikes.Where(l => _db.ReelComments.Any(c => c.Id == l.CommentId && c.ReelId == id)).ExecuteDeleteAsync();
            await _db.ReelComments.Where(c => c.ReelId == id).ExecuteDeleteAsync();
            await _db.Notifications.Where(n => n.ReelId == id).ExecuteDeleteAsync();

            try
            {
                var path = Path.Combine(_env.WebRootPath, "uploads", "reels", Path.GetFileName(reel.VideoPath));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reel faylı silinmədi: {Path}", reel.VideoPath);
            }
            return NoContent();
        }

        // ================= BƏYƏNMƏ =================

        /// <summary>Bəyən / bəyənməni geri al (toggle). like=true|false verilsə — həmin vəziyyətə gətirir.</summary>
        [HttpPost("{id:int}/like")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ReelLikeResultDto>> Like(int id, [FromQuery] bool? like)
        {
            var reel = await _db.Reels.FirstOrDefaultAsync(r => r.Id == id && !r.SoftDeleted);
            if (reel == null) return Error(404, L.T("Video tapılmadı."));

            var me = Me!;
            var existing = await _db.ReelLikes.FirstOrDefaultAsync(l => l.ReelId == id && l.UserName == me);
            var want = like ?? existing == null;
            if (want && existing == null)
                _db.ReelLikes.Add(new ReelLike { ReelId = id, UserName = me, CreatedDate = AzTime.Now });
            else if (!want && existing != null)
                _db.ReelLikes.Remove(existing);

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException) { /* eyni anda iki toxunuş — unikal indeks; vəziyyət artıq düzgündür */ }

            reel.LikeCount = await _db.ReelLikes.CountAsync(l => l.ReelId == id);
            await _db.SaveChangesAsync();

            // Bildiriş: videonun müəllifinə (özünü bəyənəndə yox). Bəyənmə geri alınsa — bildiriş də silinir.
            if (want && existing == null)
                await NotifyAsync(reel.AuthorUserName, "reel_like", reel.Id, null, null);
            else if (!want && existing != null)
                await _db.Notifications.Where(n => n.Type == "reel_like" && n.ReelId == id && n.ActorUserName == me).ExecuteDeleteAsync();

            return Ok(new ReelLikeResultDto(want, reel.LikeCount));
        }

        // ================= RƏYLƏR =================

        /// <summary>
        /// Rəylər — yuxarı səviyyəlilər ən yenidən köhnəyə, hər birinin cavabları içində (köhnədən yeniyə).
        /// Növbəti səhifə: ?before={son yuxarı səviyyəli rəyin Id-si}.
        /// </summary>
        [HttpGet("{id:int}/comments")]
        [AllowAnonymous]
        public async Task<ActionResult<ReelCommentPageDto>> Comments(int id, [FromQuery] int? before, [FromQuery] int take = 30)
        {
            take = Math.Clamp(take, 1, 100);
            var total = await _db.ReelComments.CountAsync(c => c.ReelId == id);
            var q = _db.ReelComments.AsNoTracking().Where(c => c.ReelId == id && c.ParentId == null);
            if (before is > 0) q = q.Where(c => c.Id < before);
            var rows = await q.OrderByDescending(c => c.Id).Take(take + 1).ToListAsync();
            var hasMore = rows.Count > take;
            if (hasMore) rows.RemoveAt(rows.Count - 1);

            var parentIds = rows.Select(c => c.Id).ToList();
            var replies = await _db.ReelComments.AsNoTracking().Where(c => c.ParentId != null && parentIds.Contains(c.ParentId.Value))
                .OrderBy(c => c.Id).ToListAsync();
            var users = await UsersAsync(rows.Concat(replies).SelectMany(c => new[] { c.UserName, c.ReplyToUserName ?? c.UserName }));
            var byParent = replies.ToLookup(r => r.ParentId!.Value);
            var me = Me;
            var allIds = rows.Select(c => c.Id).Concat(replies.Select(r => r.Id)).ToList();
            _likedComments = me == null ? []
                : (await _db.ReelCommentLikes.Where(l => allIds.Contains(l.CommentId) && l.UserName == me).Select(l => l.CommentId).ToListAsync()).ToHashSet();

            return Ok(new ReelCommentPageDto(
                rows.Select(c => ToDto(c, users, byParent[c.Id].Select(r => ToDto(r, users, [])).ToList())).ToList(),
                hasMore, total));
        }

        /// <summary>Rəy yaz: mətn və ya stiker; ParentId verilsə — cavab.</summary>
        [HttpPost("{id:int}/comments")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ReelCommentDto>> AddComment(int id, [FromBody] ReelCommentRequest request)
        {
            var text = Clean(request.Text, 1000);
            var sticker = string.IsNullOrWhiteSpace(request.Sticker) ? null : request.Sticker.Trim();
            if (sticker != null && ReelStickers.Emoji(sticker) == null) return Error(400, L.T("Belə stiker yoxdur."));
            if (string.IsNullOrEmpty(text) && sticker == null) return Error(400, L.T("Rəy boş ola bilməz."));

            var reel = await _db.Reels.FirstOrDefaultAsync(r => r.Id == id && !r.SoftDeleted);
            if (reel == null) return Error(404, L.T("Video tapılmadı."));

            // Cavab: həmişə yuxarı səviyyəli rəyə bağlanır; cavaba cavabda "@ad" saxlanılır
            ReelComment? target = null;
            if (request.ParentId is > 0)
            {
                target = await _db.ReelComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ParentId && c.ReelId == id);
                if (target == null) return Error(404, L.T("Rəy tapılmadı."));
            }

            var comment = new ReelComment
            {
                ReelId = id,
                ParentId = target == null ? null : target.ParentId ?? target.Id,
                ReplyToUserName = target?.UserName,
                UserName = Me!,
                Text = text ?? "",
                Sticker = sticker,
                CreatedDate = AzTime.Now
            };
            _db.ReelComments.Add(comment);
            await _db.SaveChangesAsync();

            reel.CommentCount = await _db.ReelComments.CountAsync(c => c.ReelId == id);
            await _db.SaveChangesAsync();

            // Bildirişlər: cavab verilən şəxsə və videonun müəllifinə (eyni adam iki dəfə almasın)
            var snippet = Snippet(comment);
            if (target != null)
                await NotifyAsync(target.UserName, "comment_reply", id, comment.Id, snippet);
            if (target == null || !string.Equals(target.UserName, reel.AuthorUserName, StringComparison.OrdinalIgnoreCase))
                await NotifyAsync(reel.AuthorUserName, "reel_comment", id, comment.Id, snippet);

            var users = await UsersAsync([comment.UserName, comment.ReplyToUserName ?? comment.UserName]);
            return Ok(ToDto(comment, users, []));
        }

        /// <summary>Öz rəyini redaktə et (yalnız mətn; stikeri dəyişmək olmur).</summary>
        [HttpPut("comments/{commentId:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ReelCommentDto>> EditComment(int commentId, [FromBody] ReelCommentUpdateRequest request)
        {
            var comment = await _db.ReelComments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null) return Error(404, L.T("Rəy tapılmadı."));
            if (!string.Equals(comment.UserName, Me, StringComparison.OrdinalIgnoreCase))
                return Error(403, L.T("Yalnız öz rəyinizi redaktə edə bilərsiniz."));
            if (comment.Sticker != null) return Error(400, L.T("Stikeri redaktə etmək olmur."));

            var text = Clean(request.Text, 1000);
            if (string.IsNullOrEmpty(text)) return Error(400, L.T("Rəy boş ola bilməz."));
            if (text != comment.Text)
            {
                comment.Text = text;
                comment.EditedDate = AzTime.Now;
                await _db.SaveChangesAsync();
                await _db.Notifications.Where(n => n.CommentId == commentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.Snippet, Snippet(comment)));
            }

            var users = await UsersAsync([comment.UserName, comment.ReplyToUserName ?? comment.UserName]);
            return Ok(ToDto(comment, users, []));
        }

        /// <summary>Rəyi sil (müəllif və ya admin). Yuxarı səviyyəli rəy silinəndə cavabları da silinir.</summary>
        [HttpDelete("comments/{commentId:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var comment = await _db.ReelComments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null) return Error(404, L.T("Rəy tapılmadı."));
            if (!IsAdmin && !string.Equals(comment.UserName, Me, StringComparison.OrdinalIgnoreCase))
                return Error(403, L.T("Bu rəyi silə bilməzsiniz."));

            var ids = await _db.ReelComments.Where(c => c.Id == commentId || c.ParentId == commentId).Select(c => c.Id).ToListAsync();
            await _db.Notifications.Where(n => n.CommentId != null && ids.Contains(n.CommentId.Value)).ExecuteDeleteAsync();
            await _db.ReelCommentLikes.Where(l => ids.Contains(l.CommentId)).ExecuteDeleteAsync();
            await _db.ReelComments.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync();

            var count = await _db.ReelComments.CountAsync(c => c.ReelId == comment.ReelId);
            await _db.Reels.Where(r => r.Id == comment.ReelId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CommentCount, count));
            return NoContent();
        }

        // ================= RƏYİ BƏYƏN =================

        /// <summary>Rəyi bəyən / geri al (toggle; like=true|false — həmin vəziyyətə). Rəyin müəllifinə bildiriş gedir.</summary>
        [HttpPost("comments/{commentId:int}/like")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ReelLikeResultDto>> LikeComment(int commentId, [FromQuery] bool? like)
        {
            var comment = await _db.ReelComments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null) return Error(404, L.T("Rəy tapılmadı."));

            var me = Me!;
            var existing = await _db.ReelCommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserName == me);
            var want = like ?? existing == null;
            if (want && existing == null)
                _db.ReelCommentLikes.Add(new ReelCommentLike { CommentId = commentId, UserName = me, CreatedDate = AzTime.Now });
            else if (!want && existing != null)
                _db.ReelCommentLikes.Remove(existing);

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException) { /* eyni anda iki toxunuş — unikal indeks */ }

            comment.LikeCount = await _db.ReelCommentLikes.CountAsync(l => l.CommentId == commentId);
            await _db.SaveChangesAsync();

            if (want && existing == null)
                await NotifyAsync(comment.UserName, "comment_like", comment.ReelId, comment.Id, Snippet(comment));
            else if (!want && existing != null)
                await _db.Notifications.Where(n => n.Type == "comment_like" && n.CommentId == commentId && n.ActorUserName == me).ExecuteDeleteAsync();

            return Ok(new ReelLikeResultDto(want, comment.LikeCount));
        }

        // ================= BİLDİRİŞ =================

        private async Task NotifyAsync(string to, string type, int reelId, int? commentId, string? snippet)
        {
            var me = Me;
            if (me == null || string.Equals(to, me, StringComparison.OrdinalIgnoreCase)) return;   // özünə bildiriş yox
            _db.Notifications.Add(new AppNotification
            {
                UserName = to,
                ActorUserName = me,
                Type = type,
                ReelId = reelId,
                CommentId = commentId,
                Snippet = snippet,
                CreatedDate = AzTime.Now
            });
            await _db.SaveChangesAsync();
        }

        private static string Snippet(ReelComment c)
        {
            var s = ReelStickers.Emoji(c.Sticker) ?? c.Text;
            return s.Length > 120 ? s[..120] + "…" : s;
        }

        // ================= KÖMƏKÇİLƏR =================

        private async Task<List<ReelDto>> ToDtosAsync(IReadOnlyList<Reel> reels)
        {
            var ids = reels.Select(r => r.Id).ToList();
            var me = Me;
            var liked = me == null
                ? new HashSet<int>()
                : (await _db.ReelLikes.Where(l => ids.Contains(l.ReelId) && l.UserName == me).Select(l => l.ReelId).ToListAsync()).ToHashSet();
            var users = await UsersAsync(reels.Select(r => r.AuthorUserName));
            var admin = IsAdmin;

            return reels.Select(r =>
            {
                users.TryGetValue(r.AuthorUserName, out var u);
                return new ReelDto(r.Id, "/uploads/reels/" + r.VideoPath, r.Caption,
                    string.IsNullOrWhiteSpace(u.FullName) ? "DoctorEngine" : u.FullName,
                    u.Avatar, Iso(r.CreatedDate), r.LikeCount, r.CommentCount, r.ViewCount,
                    liked.Contains(r.Id), admin);
            }).ToList();
        }

        private ReelCommentDto ToDto(ReelComment c, Dictionary<string, (string FullName, string? Avatar)> users, IReadOnlyList<ReelCommentDto> replies)
        {
            users.TryGetValue(c.UserName, out var u);
            string? replyTo = null;
            if (c.ReplyToUserName != null)
                replyTo = users.TryGetValue(c.ReplyToUserName, out var r) && !string.IsNullOrWhiteSpace(r.FullName) ? r.FullName : c.ReplyToUserName;
            var mine = string.Equals(c.UserName, Me, StringComparison.OrdinalIgnoreCase);
            return new ReelCommentDto(c.Id, c.ParentId, c.UserName, string.IsNullOrWhiteSpace(u.FullName) ? c.UserName : u.FullName,
                u.Avatar, c.Text, c.Sticker, replyTo, Iso(c.CreatedDate), c.EditedDate != null,
                mine && c.Sticker == null, mine || IsAdmin, replies, c.LikeCount, _likedComments.Contains(c.Id));
        }

        private async Task<Dictionary<string, (string FullName, string? Avatar)>> UsersAsync(IEnumerable<string> names)
        {
            var list = names.Distinct().ToList();
            var rows = await _db.Users.AsNoTracking().Where(u => list.Contains(u.UserName!))
                .Select(u => new { u.UserName, u.FullName, u.ProfileImage }).ToListAsync();
            return rows.ToDictionary(u => u.UserName!,
                u => (u.FullName ?? u.UserName!, string.IsNullOrEmpty(u.ProfileImage) ? null : "/uploads/avatars/" + u.ProfileImage),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string? Clean(string? text, int max)
        {
            var t = text?.Trim();
            if (string.IsNullOrEmpty(t)) return null;
            return t.Length > max ? t[..max] : t;
        }

        private static string Iso(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss");

        private ObjectResult Error(int code, string message) => StatusCode(code, new ApiError(message, code));
    }
}
