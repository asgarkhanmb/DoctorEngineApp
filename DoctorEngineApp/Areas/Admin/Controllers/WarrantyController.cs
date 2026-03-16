using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Warranties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class WarrantyController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IWarrantyService _service;

    public WarrantyController(
        AppDbContext db,
        IWebHostEnvironment env,
        IWarrantyService service)
    {
        _db = db;
        _env = env;
        _service = service;
    }
    public async Task<IActionResult> Index()
    {
        var list = await _service.GetAllAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateJob(WarrantyCreateVM model)
    {
        if (!ModelState.IsValid)
        {
            model.Logos = await _db.WarrantyLogos
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Name
                }).ToListAsync();

            return View(model);
        }

        var selectedJobs = model.Jobs
            .Where(j => j.IsSelected)
            .ToList();

        // burada DB-yə save edəcəksən

        return RedirectToAction("Index");
    }


    [HttpPost]
    public async Task<IActionResult> Create(WarrantyCreateVM vm)
    {
        if (!ModelState.IsValid)
        {
            // ❗ ƏN VACİB HİSSƏ
            vm.Jobs = await _db.JobCatalogs
                .Select(j => new JobVM
                {
                    JobId = j.Id,
                    Name = j.Name,
                    WarrantyMonths = j.DefaultWarrantyMonths
                })
                .ToListAsync();

            vm.Logos = await _db.WarrantyLogos
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Name
                })
                .ToListAsync();

            return View(vm);
        }

        // 🔹 LOGO UPLOAD
        if (vm.LogoFile != null)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "logos");

            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            var fileName = Guid.NewGuid() + Path.GetExtension(vm.LogoFile.FileName);
            var fullPath = Path.Combine(uploadsRoot, fileName);

            using var fs = new FileStream(fullPath, FileMode.Create);
            await vm.LogoFile.CopyToAsync(fs);

            var logo = new WarrantyLogo
            {
                Name = "Custom Logo",
                ImagePath = "/uploads/logos/" + fileName
            };

            _db.WarrantyLogos.Add(logo);
            await _db.SaveChangesAsync();

            vm.WarrantyLogoId = logo.Id;
        }

        await _service.CreateAsync(vm);
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Pdf(int id)
    {
        var data = await _service.GetAsync(id);
        return new Rotativa.AspNetCore.ViewAsPdf("Pdf", data);
    }
}
