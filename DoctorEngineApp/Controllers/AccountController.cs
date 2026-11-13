using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;

namespace DoctorEngineApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private const long MaxAvatarBytes = 5L * 1024 * 1024;   // 5 MB

        public AccountController(UserManager<AppUser> userManager,
                                 SignInManager<AppUser> signInManager,
                                 RoleManager<IdentityRole> roleManager,
                                 IEmailService emailService,
                                 AppDbContext context,
                                 IWebHostEnvironment env)
        {
            _env = env;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _context = context;
        }

        // REGISTER
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new AppUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.UserName
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            // Role yaradılır (Member)
            if (!await _roleManager.RoleExistsAsync(nameof(Roles.Member)))
                await _roleManager.CreateAsync(new IdentityRole(nameof(Roles.Member)));

            await _userManager.AddToRoleAsync(user, nameof(Roles.Member));

            // Email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var url = Url.Action(nameof(ConfirmEmail),
                                 "Account",
                                 new { userId = user.Id, token },
                                 Request.Scheme);

            // İstifadəçi daxil etdiyi məlumat HTML-ə encode olunaraq yazılır
            string html = $"<h3>Xoş Gəlmisiniz {HtmlEncoder.Default.Encode(user.FullName)}</h3>" +
                          $"<p>Emailinizi təsdiqləyin:</p>" +
                          $"<a href=\"{HtmlEncoder.Default.Encode(url ?? string.Empty)}\">Təsdiqlə</a>";

            await _emailService.SendAsync(user.Email, "Email Confirmation", html);

            return RedirectToAction(nameof(VerifyEmail));
        }

        // EMAIL CONFIRMATION
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest();

            return RedirectToAction(nameof(Login));
        }

        // VERIFY EMAIL PAGE
        [HttpGet]
        public IActionResult VerifyEmail() => View();

        // LOGIN
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.EmailOrUsername)
                       ?? await _userManager.FindByNameAsync(model.EmailOrUsername);

            // Eyni ümumi mesaj: hansı hesabın mövcud olduğunu bildirmirik
            const string invalidLogin = "İstifadəçi adı/email və ya şifrə yanlışdır";

            if (user == null)
            {
                ModelState.AddModelError("", invalidLogin);
                return View(model);
            }

            // Şifrə əvvəlcə yoxlanılır; lockoutOnFailure: true -> brute-force qoruması
            var result = await _signInManager.PasswordSignInAsync(user,
                                                                  model.Password,
                                                                  model.RememberMe,
                                                                  lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Çox sayda uğursuz cəhd. Hesab müvəqqəti bloklanıb, bir neçə dəqiqə sonra yenidən cəhd edin.");
                return View(model);
            }

            // RequireConfirmedEmail aktiv olduğu üçün təsdiqlənməmiş email IsNotAllowed qaytarır
            // (yalnız şifrə düzgün olduqda bura çatır)
            if (result.IsNotAllowed)
            {
                ModelState.AddModelError("", "Email təsdiqlənməyib. Zəhmət olmasa emailinizi yoxlayın.");
                return View(model);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", invalidLogin);
                return View(model);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // LOGOUT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // FORGOT PASSWORD
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var url = Url.Action(nameof(ResetPassword),
                                     "Account",
                                     new { email = user.Email, token },
                                     Request.Scheme);

                string html = $"<h3>Şifrəni yenilə</h3>" +
                              $"<p>Yeni şifrə təyin etmək üçün aşağıdakı linkə klik edin:</p>" +
                              $"<a href=\"{HtmlEncoder.Default.Encode(url ?? string.Empty)}\">Şifrəni dəyiş</a>";

                await _emailService.SendAsync(user.Email, "Reset Password", html);
            }

            // Mesaj göstərmək üçün TempData
            TempData["SuccessMessage"] = "Şifrə yeniləmə linki emailinizə göndərildi.";

            return RedirectToAction(nameof(Login));
        }

        // RESET PASSWORD
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (email == null || token == null)
                return BadRequest();

            var model = new ResetPasswordVM
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction(nameof(Login));

            var result = await _userManager.ResetPasswordAsync(user,
                                                               model.Token,
                                                               model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            // Şifrə yeniləndikdən sonra uğursuz cəhdlər və blok sıfırlanır
            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);

            // Avtomatik login
            await _signInManager.SignInAsync(user, false);

            return RedirectToAction("Index", "Home");
        }

        // =========================================================
        // PROFİL
        // =========================================================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            return View(BuildProfile(user));
        }

        // Ad Soyad + istifadəçi adı
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Info")] ProfileInfoVM model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
                return ProfileView(user, info: model);

            var newUserName = model.UserName.Trim();
            var oldUserName = user.UserName ?? string.Empty;
            bool userNameChanged = !string.Equals(oldUserName, newUserName, StringComparison.Ordinal);

            await using var tx = await _context.Database.BeginTransactionAsync();

            user.FullName = model.FullName.Trim();

            if (userNameChanged)
            {
                // SetUserNameAsync unikallığı yoxlayır və NormalizedUserName-i yeniləyir
                var nameResult = await _userManager.SetUserNameAsync(user, newUserName);
                if (!nameResult.Succeeded)
                {
                    foreach (var error in nameResult.Errors)
                        ModelState.AddModelError("Info.UserName", error.Description);
                    return ProfileView(user, info: model);
                }

                // Çat mesajları istifadəçi adına bağlıdır — köhnə mesajlar itməsin
                var messages = await _context.ChatMessages
                    .Where(m => m.UserName == oldUserName)
                    .ToListAsync();
                foreach (var m in messages)
                    m.UserName = newUserName;
                await _context.SaveChangesAsync();
            }
            else
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                        ModelState.AddModelError("Info.FullName", error.Description);
                    return ProfileView(user, info: model);
                }
            }

            await tx.CommitAsync();

            // Cookie-dəki istifadəçi adı yenilənsin
            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSuccess"] = "Profil məlumatlarınız yeniləndi.";
            return RedirectToAction(nameof(Profile));
        }

        // Email dəyişikliyi: yeni emailə təsdiq linki göndərilir, email yalnız təsdiqdən sonra dəyişir
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmail([Bind(Prefix = "Email")] ChangeEmailVM model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
                return ProfileView(user, email: model);

            var newEmail = model.NewEmail.Trim();

            if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
            {
                ModelState.AddModelError("Email.CurrentPassword", "Şifrə yanlışdır");
                return ProfileView(user, email: model);
            }

            if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Email.NewEmail", "Bu, hazırkı email ünvanınızdır");
                return ProfileView(user, email: model);
            }

            var existing = await _userManager.FindByEmailAsync(newEmail);
            if (existing != null && existing.Id != user.Id)
            {
                ModelState.AddModelError("Email.NewEmail", "Bu email artıq başqa hesabda istifadə olunur");
                return ProfileView(user, email: model);
            }

            // Token yeni emailə bağlıdır — linkdəki email dəyişdirilsə, təsdiq alınmaz
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
            var url = Url.Action(nameof(ConfirmEmailChange),
                                 "Account",
                                 new { userId = user.Id, email = newEmail, token },
                                 Request.Scheme);

            string html = $"<h3>Salam {HtmlEncoder.Default.Encode(user.FullName ?? user.UserName ?? string.Empty)}</h3>" +
                          $"<p>Hesabınızın email ünvanını <strong>{HtmlEncoder.Default.Encode(newEmail)}</strong> olaraq dəyişmək üçün aşağıdakı linkə klik edin:</p>" +
                          $"<a href=\"{HtmlEncoder.Default.Encode(url ?? string.Empty)}\">Yeni emaili təsdiqlə</a>" +
                          $"<p>Bu sorğunu siz göndərməmisinizsə, bu məktubu nəzərə almayın.</p>";

            await _emailService.SendAsync(newEmail, "Email dəyişikliyinin təsdiqi", html);

            TempData["ProfileSuccess"] = $"Təsdiq linki {newEmail} ünvanına göndərildi. Email linkə klik etdikdən sonra dəyişəcək.";
            return RedirectToAction(nameof(Profile), null, null, "email");
        }

        // Yeni emaildəki link — başqa cihazdan da açıla bilər, ona görə [Authorize] yoxdur
        [HttpGet]
        public async Task<IActionResult> ConfirmEmailChange(string userId, string email, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var oldEmail = user.Email;

            // ChangeEmailAsync tokeni yoxlayır, emaili dəyişir və EmailConfirmed = true edir
            var result = await _userManager.ChangeEmailAsync(user, email, token);
            if (!result.Succeeded)
            {
                // Daxil olmayıbsa, Profile login-ə yönləndirir; mesaj girişdən sonra profildə görünür
                TempData["ProfileError"] = "Təsdiq linki etibarsızdır və ya vaxtı keçib. Yenidən cəhd edin.";
                return RedirectToAction(nameof(Profile), null, null, "email");
            }

            // Köhnə emailə xəbərdarlıq (hesab oğurlanmasının tez aşkarlanması üçün)
            if (!string.IsNullOrEmpty(oldEmail))
            {
                string notice = "<h3>Email ünvanınız dəyişdirildi</h3>" +
                                $"<p>Hesabınızın email ünvanı <strong>{HtmlEncoder.Default.Encode(email)}</strong> olaraq dəyişdirildi.</p>" +
                                "<p>Bu dəyişikliyi siz etməmisinizsə, dərhal bizimlə əlaqə saxlayın.</p>";
                try { await _emailService.SendAsync(oldEmail, "Email ünvanınız dəyişdirildi", notice); }
                catch { /* bildiriş göndərilməsə də dəyişiklik qüvvədədir */ }
            }

            // Eyni istifadəçi bu brauzerdə daxil olubsa, cookie yenilənsin
            if (_signInManager.IsSignedIn(User) && _userManager.GetUserId(User) == user.Id)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["ProfileSuccess"] = "Email ünvanınız uğurla dəyişdirildi.";
                return RedirectToAction(nameof(Profile), null, null, "email");
            }

            TempData["SuccessMessage"] = "Email ünvanınız dəyişdirildi. Yeni email ilə daxil ola bilərsiniz.";
            return RedirectToAction(nameof(Login));
        }

        // Şifrə dəyişikliyi
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordVM model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
                return ProfileView(user, password: new ChangePasswordVM());

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    var key = error.Code == "PasswordMismatch" ? "Password.CurrentPassword" : "Password.NewPassword";
                    ModelState.AddModelError(key, error.Description);
                }
                return ProfileView(user, password: new ChangePasswordVM());
            }

            // Şifrə dəyişəndə security stamp yenilənir — bu sessiya aktiv qalsın
            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSuccess"] = "Şifrəniz uğurla dəyişdirildi.";
            return RedirectToAction(nameof(Profile), null, null, "password");
        }

        // Profil şəkli yüklə / dəyiş
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile? photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (photo == null || photo.Length == 0)
            {
                TempData["ProfileError"] = "Şəkil seçin.";
                return RedirectToAction(nameof(Profile));
            }

            // Uzantı, MIME, ölçü və həqiqi məzmun (magic bytes) yoxlanılır
            if (!AllowedUploadAttribute.IsAllowed(photo, UploadKind.Image, out var error, MaxAvatarBytes))
            {
                TempData["ProfileError"] = error;
                return RedirectToAction(nameof(Profile));
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(photo.FileName).ToLowerInvariant();
            await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew))
                await photo.CopyToAsync(stream);

            var oldImage = user.ProfileImage;
            user.ProfileImage = fileName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                DeleteAvatarFile(fileName);
                TempData["ProfileError"] = "Şəkil yadda saxlanılmadı. Yenidən cəhd edin.";
                return RedirectToAction(nameof(Profile));
            }

            DeleteAvatarFile(oldImage);
            await _signInManager.RefreshSignInAsync(user);   // header-dəki avatar yenilənsin

            TempData["ProfileSuccess"] = "Profil şəkliniz yeniləndi.";
            return RedirectToAction(nameof(Profile));
        }

        // Profil şəklini sil
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAvatar()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (!string.IsNullOrEmpty(user.ProfileImage))
            {
                var oldImage = user.ProfileImage;
                user.ProfileImage = null;
                await _userManager.UpdateAsync(user);
                DeleteAvatarFile(oldImage);
                await _signInManager.RefreshSignInAsync(user);
                TempData["ProfileSuccess"] = "Profil şəkli silindi.";
            }

            return RedirectToAction(nameof(Profile));
        }

        private void DeleteAvatarFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            // Yalnız fayl adı — "../" kimi yollarla qovluqdan çıxmaq mümkün olmasın
            var path = Path.Combine(_env.WebRootPath, "uploads", "avatars", Path.GetFileName(fileName));
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch (IOException) { /* fayl silinməsə də profil düzgün işləyir */ }
        }

        private static ProfileVM BuildProfile(AppUser user) => new()
        {
            CurrentEmail = user.Email ?? string.Empty,
            ProfileImage = user.ProfileImage,
            Info = new ProfileInfoVM
            {
                FullName = user.FullName ?? string.Empty,
                UserName = user.UserName ?? string.Empty
            }
        };

        // Xəta olduqda profil səhifəsini göndərilən forma məlumatları ilə yenidən göstərir
        private IActionResult ProfileView(AppUser user,
                                          ProfileInfoVM? info = null,
                                          ChangeEmailVM? email = null,
                                          ChangePasswordVM? password = null)
        {
            var vm = BuildProfile(user);
            if (info != null) vm.Info = info;
            if (email != null) vm.Email = new ChangeEmailVM { NewEmail = email.NewEmail };   // şifrə geri yazılmır
            if (password != null) vm.Password = password;
            return View(nameof(Profile), vm);
        }
    }
}