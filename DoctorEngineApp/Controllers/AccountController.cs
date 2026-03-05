using DoctorEngineApp.Helpers.Enums;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVM model)
        {
            if (!ModelState.IsValid) return View(model);

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
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // Role yoxla və əlavə et
            if (!await _roleManager.RoleExistsAsync(nameof(Roles.Member)))
                await _roleManager.CreateAsync(new IdentityRole(nameof(Roles.Member)));

            await _userManager.AddToRoleAsync(user, nameof(Roles.Member));

            // Email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var url = Url.Action(nameof(ConfirmEmail), "Account",
                        new { userId = user.Id, token },
                        Request.Scheme);

            string html = $"<h3>Welcome {user.FullName}</h3>" +
                          $"<p>Please confirm your email:</p>" +
                          $"<a href='{url}'>Confirm Email</a>";

            await _emailService.SendAsync(user.Email, "Email Confirmation", html);

            return RedirectToAction(nameof(VerifyEmail));
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null) return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded) return BadRequest();

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult VerifyEmail() => View();

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.EmailOrUsername)
                        ?? await _userManager.FindByNameAsync(model.EmailOrUsername);

            if (user == null || !user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "Login failed or email not confirmed");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Login failed");
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}