using DoctorEngine.Api.Infrastructure;
using DoctorEngineApp.Localization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Tətbiqdən profil redaktəsi — saytın "Profilim" səhifəsi (AccountController) ilə eyni qaydalar:
    /// ad / istifadəçi adı (çat mesajları da yeni ada köçürülür), email (təsdiq linki ilə), şifrə, profil şəkli.
    /// Ad və ya şifrə dəyişəndən sonra tətbiq refresh token ilə yeni access token alır (claim-lər yenilənsin).
    /// </summary>
    [ApiController]
    [Route("api/v1/profile")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [IgnoreAntiforgeryToken]   // yalnız Bearer — CSRF riski yoxdur
    public partial class ProfileController : ControllerBase
    {
        private const long MaxAvatarBytes = 5L * 1024 * 1024;   // saytla eyni: 5 MB

        private readonly UserManager<AppUser> _users;
        private readonly AppDbContext _db;
        private readonly IEmailService _email;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(UserManager<AppUser> users, AppDbContext db, IEmailService email,
                                 IWebHostEnvironment env, ILogger<ProfileController> logger)
        {
            _users = users;
            _db = db;
            _email = email;
            _env = env;
            _logger = logger;
        }

        // ================= AD SOYAD + İSTİFADƏÇİ ADI =================

        [HttpPut]
        public async Task<ActionResult<UserDto>> Update([FromBody] UpdateProfileRequest request)
        {
            var user = await CurrentUserAsync();
            if (user == null) return Error(401, L.T("İstifadəçi tapılmadı."));

            var fullName = request.FullName?.Trim() ?? "";
            var userName = request.UserName?.Trim() ?? "";
            if (fullName.Length == 0) return Error(400, L.T("Ad və soyadı daxil edin"));
            if (fullName.Length > 100) return Error(400, L.T("Ad Soyad maksimum 100 simvol ola bilər"));
            if (userName.Length < 3 || userName.Length > 30) return Error(400, L.T("İstifadəçi adı 3–30 simvol olmalıdır"));
            if (!UserNameRegex().IsMatch(userName)) return Error(400, L.T("Yalnız latın hərfləri, rəqəm və . _ - istifadə edin"));

            var oldUserName = user.UserName ?? "";
            var changed = !string.Equals(oldUserName, userName, StringComparison.Ordinal);

            await using var tx = await _db.Database.BeginTransactionAsync();
            user.FullName = fullName;

            if (changed)
            {
                // SetUserNameAsync unikallığı yoxlayır və NormalizedUserName-i yeniləyir
                var r = await _users.SetUserNameAsync(user, userName);
                if (!r.Succeeded) return Error(400, r.Errors.First().Description);

                // Çat mesajları istifadəçi adına bağlıdır — köhnə mesajlar itməsin
                await _db.ChatMessages.Where(m => m.UserName == oldUserName)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.UserName, userName));
                await _db.ChatMessages.Where(m => m.ConversationUserName == oldUserName)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.ConversationUserName, userName));
                await _db.ChatConversations.Where(c => c.UserName == oldUserName)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UserName, userName));
                await _db.ChatReactions.Where(r => r.UserName == oldUserName)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.UserName, userName));
            }
            else
            {
                var r = await _users.UpdateAsync(user);
                if (!r.Succeeded) return Error(400, r.Errors.First().Description);
            }
            await tx.CommitAsync();

            return Ok(await ToDtoAsync(user));
        }

        // ================= EMAIL (təsdiq linki ilə) =================

        [HttpPost("email")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> ChangeEmail([FromBody] ChangeEmailRequest request)
        {
            var user = await CurrentUserAsync();
            if (user == null) return Error(401, L.T("İstifadəçi tapılmadı."));

            var newEmail = request.NewEmail?.Trim() ?? "";
            if (newEmail.Length == 0 || !new EmailAddressAttribute().IsValid(newEmail))
                return Error(400, L.T("Email düzgün formatda deyil"));
            if (string.IsNullOrEmpty(request.CurrentPassword) || !await _users.CheckPasswordAsync(user, request.CurrentPassword))
                return Error(400, L.T("Şifrə yanlışdır"));
            if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                return Error(400, L.T("Bu, hazırkı email ünvanınızdır"));

            var existing = await _users.FindByEmailAsync(newEmail);
            if (existing != null && existing.Id != user.Id)
                return Error(400, L.T("Bu email artıq başqa hesabda istifadə olunur"));

            // Link saytın səhifəsinə aparır (AccountController.ConfirmEmailChange) — saytdakı ilə eyni axın
            var token = await _users.GenerateChangeEmailTokenAsync(user, newEmail);
            var url = this.SiteLink("Account/ConfirmEmailChange", new { userId = user.Id, email = newEmail, token });
            // Məktub istifadəçinin seçdiyi dildə (tətbiqin Accept-Language başlığı)
            var html = $"<h3>{L.T("Salam {0}", HtmlEncoder.Default.Encode(user.FullName ?? user.UserName ?? ""))}</h3>" +
                       $"<p>{L.T("Hesabınızın email ünvanını {0} olaraq dəyişmək üçün aşağıdakı linkə klik edin:", $"<strong>{HtmlEncoder.Default.Encode(newEmail)}</strong>")}</p>" +
                       $"<a href=\"{HtmlEncoder.Default.Encode(url ?? "")}\">{L.T("Yeni emaili təsdiqlə")}</a>" +
                       $"<p>{L.T("Bu sorğunu siz göndərməmisinizsə, bu məktubu nəzərə almayın.")}</p>";

            try
            {
                await _email.SendAsync(newEmail, L.T("Email dəyişikliyinin təsdiqi"), html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email dəyişikliyi məktubu göndərilmədi → {To}", newEmail);
                return Error(503, L.T("Təsdiq məktubu göndərilmədi — email xidməti müvəqqəti işləmir. Bir azdan yenidən cəhd edin."));
            }
            return Ok(new MessageResponse(L.T("Təsdiq linki {0} ünvanına göndərildi. Email linkə klik etdikdən sonra dəyişəcək.", newEmail)));
        }

        // ================= ŞİFRƏ =================

        [HttpPost("password")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await CurrentUserAsync();
            if (user == null) return Error(401, L.T("İstifadəçi tapılmadı."));
            if (string.IsNullOrEmpty(request.CurrentPassword)) return Error(400, L.T("Hazırkı şifrəni daxil edin"));
            if ((request.NewPassword?.Length ?? 0) < 6) return Error(400, L.T("Şifrə minimum 6 simvol olmalıdır"));

            var r = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword!);
            if (!r.Succeeded) return Error(400, r.Errors.First().Description);

            // Security stamp dəyişdi: digər cihazların access token-ləri etibarsız olur;
            // bu tətbiq refresh token ilə dərhal yeni token alır.
            return Ok(new MessageResponse(L.T("Şifrəniz uğurla dəyişdirildi.")));
        }

        // ================= PROFİL ŞƏKLİ =================

        [HttpPost("avatar")]
        [RequestSizeLimit(MaxAvatarBytes + 1024 * 1024)]
        public async Task<ActionResult<UserDto>> UploadAvatar(IFormFile? photo)
        {
            var user = await CurrentUserAsync();
            if (user == null) return Error(401, L.T("İstifadəçi tapılmadı."));
            if (photo == null || photo.Length == 0) return Error(400, L.T("Şəkil seçin."));

            // Uzantı, MIME, ölçü və həqiqi məzmun (magic bytes) — saytla eyni yoxlama
            if (!AllowedUploadAttribute.IsAllowed(photo, UploadKind.Image, out var error, MaxAvatarBytes))
                return Error(400, error);

            var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(folder);
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(photo.FileName).ToLowerInvariant();
            await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew))
                await photo.CopyToAsync(stream);

            var old = user.ProfileImage;
            user.ProfileImage = fileName;
            var r = await _users.UpdateAsync(user);
            if (!r.Succeeded)
            {
                DeleteAvatar(fileName);
                return Error(500, L.T("Şəkil yadda saxlanılmadı. Yenidən cəhd edin."));
            }
            DeleteAvatar(old);
            return Ok(await ToDtoAsync(user));
        }

        [HttpDelete("avatar")]
        public async Task<ActionResult<UserDto>> RemoveAvatar()
        {
            var user = await CurrentUserAsync();
            if (user == null) return Error(401, L.T("İstifadəçi tapılmadı."));
            if (!string.IsNullOrEmpty(user.ProfileImage))
            {
                var old = user.ProfileImage;
                user.ProfileImage = null;
                await _users.UpdateAsync(user);
                DeleteAvatar(old);
            }
            return Ok(await ToDtoAsync(user));
        }

        // ================= KÖMƏKÇİLƏR =================

        private async Task<AppUser?> CurrentUserAsync() =>
            await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

        private async Task<UserDto> ToDtoAsync(AppUser user) => TokenService.ToDto(user, await _users.GetRolesAsync(user));

        private void DeleteAvatar(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            // Yalnız fayl adı — "../" ilə qovluqdan çıxmaq mümkün olmasın
            var path = Path.Combine(_env.WebRootPath, "uploads", "avatars", Path.GetFileName(fileName));
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            catch (IOException) { }
        }

        private ObjectResult Error(int code, string message) => StatusCode(code, new ApiError(message, code));

        [GeneratedRegex(@"^[a-zA-Z0-9._-]+$")]
        private static partial Regex UserNameRegex();
    }
}
