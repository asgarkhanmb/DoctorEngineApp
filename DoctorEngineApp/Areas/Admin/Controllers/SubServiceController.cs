using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;

[Area("Admin")]
public class SubServiceController : Controller
{
    private readonly ISubServiceService _service;
    private readonly AppDbContext _ctx;

    public SubServiceController(ISubServiceService service, AppDbContext ctx)
    {
        _service = service;
        _ctx = ctx;
    }

    public async Task<IActionResult> Index()
    {
        var list = await _service.GetAllAsync();
        return View(list);
    }

    public IActionResult Create()
    {
        ViewBag.Categories = _ctx.ServiceCategories.ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubServiceCreateVM model)
    {
        ViewBag.Categories = _ctx.ServiceCategories.ToList();
        if (!ModelState.IsValid) return View(model);

        // VALIDATE video: max 1GB and video/* content type
        const long maxSize = 1_000_000_000;
        if (model.VideoFile != null)
        {
            if (model.VideoFile.Length > maxSize)
            {
                ModelState.AddModelError("VideoFile", "Video maksimum 1GB ola bilər.");
                return View(model);
            }
            if (!model.VideoFile.ContentType.StartsWith("video/"))
            {
                ModelState.AddModelError("VideoFile", "Yalnız video formatları qəbul edilir.");
                return View(model);
            }
        }
        await _service.CreateAsync(model);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _service.GetForEditAsync(id);
        if (vm == null) return NotFound();
        ViewBag.Categories = _ctx.ServiceCategories.ToList();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SubServiceEditVM model)
    {
        ViewBag.Categories = _ctx.ServiceCategories.ToList();

        // 🔴 Category seçilməyibsə
        if (model.CategoryId == 0)
        {
            ModelState.AddModelError("CategoryId", "Category seçilməlidir.");
        }

        // 🔴 Video yoxlamaları
        const long maxSize = 1_000_000_000; // 1GB
        if (model.VideoFile != null)
        {
            if (model.VideoFile.Length > maxSize)
            {
                ModelState.AddModelError("VideoFile", "Video maksimum 1GB ola bilər.");
            }
            else if (!model.VideoFile.ContentType.StartsWith("video/"))
            {
                ModelState.AddModelError("VideoFile", "Yalnız video formatları qəbul edilir.");
            }
        }

        // 🔴 ƏGƏR YUXARIDA ERROR VARSA
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _service.UpdateAsync(model);
        return RedirectToAction(nameof(Index));
    }


    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
