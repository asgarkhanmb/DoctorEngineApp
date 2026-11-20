using DoctorEngine.Api.Infrastructure;
using DoctorEngineApp.Localization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using DoctorEngine.Contracts;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Tətbiqlər üçün giriş / qeydiyyat. Saytın qaydaları ilə eynidir:
    /// email təsdiqi tələb olunur, 5 səhv cəhddən sonra 15 dəq blok, şifrə tələbləri, yeni istifadəçi — Member rolu.
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    [IgnoreAntiforgeryToken]   // yalnız JSON qəbul edir; cookie istifadə olunmur → CSRF riski yoxdur
    public partial class AuthController : ControllerBase
    {
        private static string InvalidLogin => L.T("İstifadəçi adı/email və ya şifrə yanlışdır");

        private readonly UserManager<AppUser> _users;
        private readonly SignInManager<AppUser> _signIn;
        private readonly RoleManager<IdentityRole> _roles;
        private readonly TokenService _tokens;
        private readonly IEmailService _email;
        private readonly JwtOptions _jwt;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn, RoleManager<IdentityRole> roles,
                              TokenService tokens, IEmailService email, IOptions<JwtOptions> jwt, ILogger<AuthController> logger)
        {
            _users = users;
            _signIn = signIn;
            _roles = roles;
            _tokens = tokens;
            _email = email;
            _jwt = jwt.Value;
            _logger = logger;
        }

        // ================= GİRİŞ =================

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            if (!_jwt.IsConfigured) return Error(503, L.T("Tətbiq girişi serverdə hələ ayarlanmayıb."));
            if (string.IsNullOrWhiteSpace(request.EmailOrUsername) || string.IsNullOrEmpty(request.Password))
                return Error(400, L.T("İstifadəçi adı/email və şifrəni daxil edin."));

            var login = request.EmailOrUsername.Trim();
            var user = await _users.FindByEmailAsync(login) ?? await _users.FindByNameAsync(login);
            if (user == null) return Error(401, InvalidLogin);   // hansı hesabın mövcud olduğunu bildirmirik

            // Saytla eyni: şifrə + lockout + email təsdiqi (cookie yaradılmır)
            var result = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (result.IsLockedOut)
                return Error(423, L.T("Çox sayda uğursuz cəhd. Hesab müvəqqəti bloklanıb, bir neçə dəqiqə sonra yenidən cəhd edin."));
            if (result.IsNotAllowed)
                return Error(403, L.T("Email təsdiqlənməyib. Zəhmət olmasa emailinizi yoxlayın."));
            if (!result.Succeeded)
                return Error(401, InvalidLogin);

            return Ok(await _tokens.IssueAsync(user, request.DeviceName));
        }

        // ================= QEYDİYYAT =================

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> Register([FromBody] RegisterRequest request)
        {
            // Saytdakı RegisterVM ilə eyni yoxlamalar
            var errors = new Dictionary<string, string[]>();
            var fullName = request.FullName?.Trim() ?? "";
            var userName = request.UserName?.Trim() ?? "";
            var email = request.Email?.Trim() ?? "";

            if (fullName.Length == 0) errors["fullName"] = new[] { L.T("Ad və soyadı daxil edin") };
            else if (fullName.Length > 100) errors["fullName"] = new[] { L.T("Ad Soyad maksimum 100 simvol ola bilər") };

            if (userName.Length < 3 || userName.Length > 30) errors["userName"] = new[] { L.T("İstifadəçi adı 3–30 simvol olmalıdır") };
            else if (!UserNameRegex().IsMatch(userName)) errors["userName"] = new[] { L.T("Yalnız latın hərfləri, rəqəm və . _ - istifadə edin") };

            if (!new EmailAddressAttribute().IsValid(email) || email.Length == 0) errors["email"] = new[] { L.T("Email düzgün formatda deyil") };

            if ((request.Password?.Length ?? 0) < 6) errors["password"] = new[] { L.T("Şifrə minimum 6 simvol olmalıdır") };

            if (errors.Count > 0)
                return BadRequest(new ApiError(errors.Values.First()[0], 400, errors));

            var user = new AppUser { FullName = fullName, Email = email, UserName = userName };
            var result = await _users.CreateAsync(user, request.Password!);
            if (!result.Succeeded)
            {
                var identityErrors = result.Errors.Select(e => e.Description).ToArray();
                return BadRequest(new ApiError(identityErrors.FirstOrDefault() ?? L.T("Qeydiyyat alınmadı."), 400,
                    new Dictionary<string, string[]> { [""] = identityErrors }));
            }

            if (!await _roles.RoleExistsAsync(nameof(Roles.Member)))
                await _roles.CreateAsync(new IdentityRole(nameof(Roles.Member)));
            await _users.AddToRoleAsync(user, nameof(Roles.Member));

            // Təsdiq linki saytın səhifəsinə aparır (tətbiqdə sonra daxil olunur)
            var token = await _users.GenerateEmailConfirmationTokenAsync(user);
            var url = this.SiteLink("Account/ConfirmEmail", new { userId = user.Id, token });
            var html = $"<h3>{L.T("Xoş Gəlmisiniz {0}", HtmlEncoder.Default.Encode(user.FullName))}</h3>" +
                       $"<p>{L.T("Emailinizi təsdiqləyin:")}</p>" +
                       $"<a href=\"{HtmlEncoder.Default.Encode(url ?? string.Empty)}\">{L.T("Təsdiqlə")}</a>";

            var sent = await TrySendAsync(email, L.T("Email təsdiqi"), html);
            return Ok(new MessageResponse(sent
                ? L.T("Hesab yaradıldı. Emailinizə göndərilən linklə hesabınızı təsdiqləyin, sonra daxil olun.")
                : L.T("Hesab yaradıldı, amma təsdiq məktubu göndərilmədi. Bir azdan «Şifrəni unutmusunuz» ilə yenidən cəhd edin.")));
        }

        // ================= TOKEN YENİLƏMƏ / ÇIXIŞ =================

        [HttpPost("refresh")]
        [AllowAnonymous]   // limitsiz: refresh token 256-bit təsadüfidir (təxmin oluna bilməz), tətbiq onu avtomatik çağırır
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
        {
            if (!_jwt.IsConfigured) return Error(503, L.T("Tətbiq girişi serverdə hələ ayarlanmayıb."));
            var response = await _tokens.RefreshAsync(request.RefreshToken, request.DeviceName);
            return response == null
                ? Error(401, L.T("Sessiyanın vaxtı bitib. Yenidən daxil olun."))
                : Ok(response);
        }

        [HttpPost("logout")]
        [AllowAnonymous]   // access token bitmiş olsa da çıxış mümkün olmalıdır
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            await _tokens.RevokeAsync(request.RefreshToken);
            return NoContent();
        }

        // ================= PROFİL =================

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<UserDto>> Me()
        {
            var user = await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (user == null) return Error(401, L.T("İstifadəçi tapılmadı."));
            return Ok(TokenService.ToDto(user, await _users.GetRolesAsync(user)));
        }

        // ================= ŞİFRƏNİ UNUTMUŞAM =================

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            // Cavab həmişə eynidir: hansı emailin qeydiyyatda olduğunu bildirmirik
            var generic = L.T("Əgər bu email qeydiyyatdadırsa, şifrə yeniləmə linki göndərildi.");
            var user = string.IsNullOrWhiteSpace(request.Email) ? null : await _users.FindByEmailAsync(request.Email.Trim());
            if (user == null || !await _users.IsEmailConfirmedAsync(user))
                return Ok(new MessageResponse(generic));

            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var url = this.SiteLink("Account/ResetPassword", new { email = user.Email, token });
            var html = $"<h3>{L.T("Şifrəni yenilə")}</h3>" +
                       $"<p>{L.T("Yeni şifrə təyin etmək üçün aşağıdakı linkə klik edin:")}</p>" +
                       $"<a href=\"{HtmlEncoder.Default.Encode(url ?? string.Empty)}\">{L.T("Şifrəni dəyiş")}</a>";

            if (!await TrySendAsync(user.Email!, L.T("Şifrənin yenilənməsi"), html))
                return Error(503, L.T("Məktub göndərilmədi — email xidməti müvəqqəti işləmir. Bir azdan yenidən cəhd edin."));
            return Ok(new MessageResponse(generic));
        }

        // ================= EMAIL TƏSDİQİ (saytdakı Account/ConfirmEmail) =================

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Token))
                return Error(400, L.T("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));

            var user = await _users.FindByIdAsync(request.UserId);
            if (user == null) return Error(404, L.T("İstifadəçi tapılmadı."));

            var result = await _users.ConfirmEmailAsync(user, request.Token);
            if (!result.Succeeded)
                return Error(400, L.T("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));

            return Ok(new MessageResponse(L.T("Email təsdiqləndi. İndi daxil ola bilərsiniz.")));
        }

        // ================= ŞİFRƏNİ YENİLƏ (saytdakı Account/ResetPassword) =================

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<AuthResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Token))
                return Error(400, L.T("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));
            if ((request.Password?.Length ?? 0) < 6)
                return Error(400, L.T("Şifrə minimum 6 simvol olmalıdır"));

            var user = await _users.FindByEmailAsync(request.Email.Trim());
            if (user == null)
                return Error(400, L.T("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));

            var result = await _users.ResetPasswordAsync(user, request.Token, request.Password!);
            if (!result.Succeeded)
            {
                var identityErrors = result.Errors.Select(e => e.Description).ToArray();
                return BadRequest(new ApiError(identityErrors.FirstOrDefault() ?? L.T("Xəta baş verdi. Yenidən cəhd edin."), 400,
                    new Dictionary<string, string[]> { [""] = identityErrors }));
            }

            // Saytla eyni: uğursuz cəhdlər və blok sıfırlanır, sonra avtomatik giriş
            await _users.ResetAccessFailedCountAsync(user);
            await _users.SetLockoutEndDateAsync(user, null);

            if (!_jwt.IsConfigured) return Error(503, L.T("Tətbiq girişi serverdə hələ ayarlanmayıb."));
            return Ok(await _tokens.IssueAsync(user, request.DeviceName));
        }

        // ================= EMAIL DƏYİŞİKLİYİNİN TƏSDİQİ (saytdakı Account/ConfirmEmailChange) =================

        [HttpPost("confirm-email-change")]
        [AllowAnonymous]   // link başqa cihazda açıla bilər
        [EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Token))
                return Error(400, L.T("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));

            var user = await _users.FindByIdAsync(request.UserId);
            if (user == null) return Error(404, L.T("İstifadəçi tapılmadı."));

            var oldEmail = user.Email;
            var result = await _users.ChangeEmailAsync(user, request.Email, request.Token);
            if (!result.Succeeded)
                return Error(400, L.T("Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin."));

            // Köhnə emailə xəbərdarlıq (saytdakı kimi)
            if (!string.IsNullOrEmpty(oldEmail))
            {
                var notice = $"<h3>{L.T("Email ünvanınız dəyişdirildi")}</h3>" +
                             $"<p>{L.T("Hesabınızın email ünvanı {0} olaraq dəyişdirildi.", $"<strong>{HtmlEncoder.Default.Encode(request.Email)}</strong>")}</p>" +
                             $"<p>{L.T("Bu dəyişikliyi siz etməmisinizsə, dərhal bizimlə əlaqə saxlayın.")}</p>";
                await TrySendAsync(oldEmail, L.T("Email ünvanınız dəyişdirildi"), notice);
            }

            return Ok(new MessageResponse(L.T("Email ünvanınız dəyişdirildi. Yeni email ilə daxil ola bilərsiniz.")));
        }

        // ================= KÖMƏKÇİLƏR =================

        private ObjectResult Error(int code, string message) =>
            StatusCode(code, new ApiError(message, code));

        private async Task<bool> TrySendAsync(string to, string subject, string html)
        {
            try
            {
                await _email.SendAsync(to, subject, html);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email göndərilmədi: {Subject} → {To}", subject, to);
                return false;
            }
        }

        [GeneratedRegex(@"^[a-zA-Z0-9._-]+$")]
        private static partial Regex UserNameRegex();
    }
}
