using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;

        public AccountController(UserManager<AppUser> userManager,
                                 SignInManager<AppUser> signInManager,
                                 RoleManager<IdentityRole> roleManager,
                                 IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
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

            string html = $"<h3>Xoş Gəlmisiniz {user.FullName}</h3>" +
                          $"<p>Emailinizi təsdiqləyin:</p>" +
                          $"<a href='{url}'>Təsdiqlə</a>";

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
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.EmailOrUsername)
                       ?? await _userManager.FindByNameAsync(model.EmailOrUsername);

            if (user == null)
            {
                ModelState.AddModelError("", "İstifadəçi tapılmadı");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError("", "Email təsdiqlənməyib");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user,
                                                                  model.Password,
                                                                  model.RememberMe,
                                                                  false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Şifrə yanlışdır");
                return View(model);
            }

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
                              $"<a href='{url}'>Şifrəni dəyiş</a>";

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

            // Avtomatik login
            await _signInManager.SignInAsync(user, false);

            return RedirectToAction("Index", "Home");
        }
    }
}