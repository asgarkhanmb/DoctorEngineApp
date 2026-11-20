using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            var now = DateTimeOffset.UtcNow;

            var list = new List<UserRoleVM>();
            foreach (var u in users)
            {
                list.Add(new UserRoleVM
                {
                    Id = u.Id,
                    Fullname = u.FullName,
                    Email = u.Email ?? "",
                    Username = u.UserName ?? "",
                    EmailConfirmed = u.EmailConfirmed,
                    IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > now,
                    RoleList = (await _userManager.GetRolesAsync(u)).ToList()
                });
            }

            return View(list);
        }

        [HttpGet]
        public IActionResult AddRole(string? userId = null)
        {
            FillLists();
            return View(new UserAddRoleVM { UsernameId = userId });
        }

        [HttpPost]
        public async Task<IActionResult> AddRole(UserAddRoleVM request)
        {
            if (!ModelState.IsValid)
            {
                FillLists();
                return View(request);
            }

            var user = await _userManager.FindByIdAsync(request.UsernameId!);
            var role = await _roleManager.FindByIdAsync(request.RoleId!);

            if (user == null || role == null)
            {
                ModelState.AddModelError(string.Empty, "İstifadəçi və ya rol tapılmadı.");
                FillLists();
                return View(request);
            }

            if (await _userManager.IsInRoleAsync(user, role.Name!))
            {
                ModelState.AddModelError(string.Empty, $"{user.UserName} artıq \"{role.Name}\" roluna malikdir.");
                FillLists();
                return View(request);
            }

            await _userManager.AddToRoleAsync(user, role.Name!);
            TempData["Success"] = $"{user.UserName} istifadəçisinə \"{role.Name}\" rolu verildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RemoveRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Admin özünü admin rolundan çıxara bilməz (paneldən kənarda qalmasın)
            if (role == "Admin" && user.Id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "Öz admin rolunuzu silə bilməzsiniz.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.RemoveFromRoleAsync(user, role);
            TempData["Success"] = $"{user.UserName} istifadəçisindən \"{role}\" rolu silindi.";
            return RedirectToAction(nameof(Index));
        }

        private void FillLists()
        {
            ViewBag.users = new SelectList(_userManager.Users.OrderBy(u => u.UserName).ToList(), "Id", "UserName");
            ViewBag.roles = new SelectList(_roleManager.Roles.OrderBy(r => r.Name).ToList(), "Id", "Name");
        }
    }
}
