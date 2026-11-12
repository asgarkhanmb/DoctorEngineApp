using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Technicians;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TechnicianController : Controller
{
    private readonly ITechnicianService _service;

    public TechnicianController(ITechnicianService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _service.GetAllAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(TechnicianCreateVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        await _service.CreateAsync(vm);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var tech = await _service.GetByIdAsync(id);

        TechnicianEditVM vm = new()
        {
            Id = tech.Id,
            FullName = tech.FullName,
            Position = tech.Position,
            ExistingImage = tech.Image,
            FacebookUrl = tech.FacebookUrl,
            InstagramUrl = tech.InstagramUrl,
            TiktokUrl = tech.TiktokUrl
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(TechnicianEditVM vm)
    {
        if (!ModelState.IsValid)
        {
            // Forma yenidən göstəriləndə mövcud şəkil itməsin
            var current = await _service.GetByIdAsync(vm.Id);
            if (current == null) return NotFound();
            vm.ExistingImage = current.Image;
            return View(vm);
        }

        await _service.EditAsync(vm);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
