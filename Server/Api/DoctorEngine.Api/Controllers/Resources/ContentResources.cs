using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Services;
using DoctorEngineApp.ViewModels.Sliders;
using DoctorEngineApp.ViewModels.Technicians;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.Resources
{
    // =====================================================================
    // Ana səhifənin məzmunu: slayderlər, üstünlüklər, haqqımızda,
    // xidmət kateqoriyaları + videolar, rezervasiya xidmətləri, ustalar.
    // Mətnlər bazadakı kimi (azərbaycanca) qaytarılır — tərcüməni front edir (api/v1/translations).
    // =====================================================================

    [Route("api/v1/sliders")]
    public class SlidersController : ResourceController
    {
        private readonly ISliderService _service;
        public SlidersController(ISliderService service) => _service = service;

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<Slider>>> List() => await _service.GetAllAsync();

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<ActionResult<Slider>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] SliderVM vm)
        {
            if (vm.ImageFile1 == null) return Error(400, "Fon şəkli məcburidir");
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] SliderVM vm)
        {
            vm.Id = id;
            await _service.UpdateAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/informs")]
    public class InformsController : ResourceController
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public InformsController(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<Inform>>> List() => await _db.Informs.AsNoTracking().ToListAsync();

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<ActionResult<Inform>> Get(int id) => OkOrNotFound(await _db.Informs.FindAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] InformCreateVM vm)
        {
            // İkon məcburidir (bazada boş ola bilməz)
            if (vm.Icon == null) return Error(400, "İkon seçin");

            _db.Informs.Add(new Inform
            {
                Icon = await SaveIconAsync(vm.Icon),
                Title = vm.Title,
                Description = vm.Description
            });
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] InformEditVM vm)
        {
            var item = await _db.Informs.FindAsync(id);
            if (item == null) return Error(404, "Qeyd tapılmadı.");

            if (vm.Icon != null)
            {
                var newIcon = await SaveIconAsync(vm.Icon);
                DeleteFile(item.Icon);
                item.Icon = newIcon;
            }
            item.Title = vm.Title;
            item.Description = vm.Description;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Informs.FindAsync(id);
            if (item == null) return Error(404, "Qeyd tapılmadı.");
            DeleteFile(item.Icon);
            _db.Informs.Remove(item);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<string> SaveIconAsync(IFormFile file)
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "inform");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using (var stream = new FileStream(Path.Combine(folder, name), FileMode.Create))
                await file.CopyToAsync(stream);
            return "/uploads/inform/" + name;
        }

        private void DeleteFile(string? webPath)
        {
            if (string.IsNullOrEmpty(webPath)) return;
            // Yalnız uploads/inform qovluğundakı fayl (../ ilə qovluqdan çıxmaq olmaz)
            var path = Path.Combine(_env.WebRootPath, "uploads", "inform", Path.GetFileName(webPath));
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { /* fayl məşğuldursa — qalsın */ }
        }
    }

    [Route("api/v1/abouts")]
    public class AboutsController : ResourceController
    {
        private readonly IAboutService _service;
        public AboutsController(IAboutService service) => _service = service;

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<About>>> List() => await _service.GetAllAsync();

        /// <summary>Saytda göstərilən tək "Haqqımızda" qeydi (yoxdursa 204).</summary>
        [HttpGet("single"), AllowAnonymous]
        public async Task<ActionResult<About?>> Single() => await _service.GetSingleAsync();

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<ActionResult<About>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] AboutCreateVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] AboutEditVM vm)
        {
            vm.Id = id;
            await _service.UpdateAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/categories")]
    public class CategoriesController : ResourceController
    {
        private readonly ICategoryService _service;
        private readonly AppDbContext _db;
        public CategoriesController(ICategoryService service, AppDbContext db) { _service = service; _db = db; }

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<CategoryVM>>> List() => await _service.GetAllAsync();

        /// <summary>Kateqoriyalar videoları ilə (ana səhifə, Xidmətlər səhifəsi — saytdakı sorğu ilə eyni sıra).</summary>
        [HttpGet("with-videos"), AllowAnonymous]
        public async Task<ActionResult<List<CategoryVM>>> WithVideos() =>
            await _db.ServiceCategories.AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new CategoryVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    Icon = c.Icon,
                    SubServices = c.SubServices.OrderBy(s => s.Id).Select(s => new SubServiceVM
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Description = s.Description,
                        Video = s.Video
                    }).ToList()
                }).ToListAsync();

        /// <summary>Ən son əlavə olunan kateqoriyalar (footer): GET ...?take=5</summary>
        [HttpGet("latest"), AllowAnonymous]
        public async Task<ActionResult<List<ServiceVM>>> Latest([FromQuery] int take = 5) =>
            await _db.ServiceCategories.AsNoTracking()
                .OrderByDescending(c => c.Id)
                .Take(Math.Clamp(take, 1, 50))
                .Select(c => new ServiceVM { Id = c.Id, Name = c.Name })
                .ToListAsync();

        [HttpGet("count"), AllowAnonymous]
        public async Task<ActionResult<int>> Count() => await _service.CountAsync();

        /// <summary>Videosu olmayan kateqoriyalar (+ istəyə görə redaktə olunan videonun öz kateqoriyası).</summary>
        [HttpGet("free")]
        public async Task<ActionResult<List<ServiceCategory>>> Free([FromQuery] int? includeId = null) =>
            await _db.ServiceCategories.AsNoTracking()
                .Where(c => !c.SubServices.Any() || c.Id == includeId)
                .OrderBy(c => c.Id)
                .Select(c => new ServiceCategory { Id = c.Id, Name = c.Name, Icon = c.Icon })
                .ToListAsync();

        [HttpGet("{id:int}/edit")]
        public async Task<ActionResult<CategoryEditVM>> Edit(int id) => OkOrNotFound(await _service.GetForEditAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CategoryCreateVM vm)
        {
            if (await _service.CountAsync() >= ServiceCategory.MaxCount)
                return Error(400, $"Maksimum {ServiceCategory.MaxCount} kateqoriya ola bilər. Yenisini əlavə etmək üçün əvvəlcə birini silin.");
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] CategoryEditVM vm)
        {
            vm.Id = id;
            await _service.UpdateAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/subservices")]
    public class SubServicesController : ResourceController
    {
        private readonly ISubServiceService _service;
        private readonly AppDbContext _db;
        public SubServicesController(ISubServiceService service, AppDbContext db) { _service = service; _db = db; }

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<SubServiceVM>>> List() => await _service.GetAllAsync();

        /// <summary>Videosu olmayan kateqoriya varmı (yeni video əlavə etmək olarmı).</summary>
        [HttpGet("has-free-category")]
        public async Task<ActionResult<bool>> HasFreeCategory() =>
            await _db.ServiceCategories.AnyAsync(c => !c.SubServices.Any());

        /// <summary>Kateqoriyanın artıq videosu varmı (exceptId — redaktə olunan video sayılmır).</summary>
        [HttpGet("category-busy")]
        public async Task<ActionResult<bool>> CategoryBusy([FromQuery] int categoryId, [FromQuery] int? exceptId = null) =>
            await _db.SubServices.AnyAsync(s => s.CategoryId == categoryId && s.Id != exceptId);

        [HttpGet("{id:int}/edit")]
        public async Task<ActionResult<SubServiceEditVM>> Edit(int id) => OkOrNotFound(await _service.GetForEditAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] SubServiceCreateVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] SubServiceEditVM vm)
        {
            vm.Id = id;
            await _service.UpdateAsync(vm);
            return NoContent();
        }

        /// <summary>Videonu boş kateqoriyaya köçür: POST ...?categoryId=3</summary>
        [HttpPost("{id:int}/move")]
        public async Task<IActionResult> Move(int id, [FromQuery] int categoryId)
        {
            var entity = await _db.SubServices.FindAsync(id);
            if (entity == null) return Error(404, "Qeyd tapılmadı.");
            var target = await _db.ServiceCategories.Include(c => c.SubServices).FirstOrDefaultAsync(c => c.Id == categoryId);
            if (target == null) return Error(404, "Kateqoriya tapılmadı.");
            if (target.SubServices.Any()) return Error(400, $"“{target.Name}” kateqoriyasının artıq videosu var.");
            entity.CategoryId = categoryId;
            await _db.SaveChangesAsync();
            return Ok(new DoctorEngine.Contracts.MessageResponse($"Video “{target.Name}” kateqoriyasına köçürüldü."));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    /// <summary>Rezervasiya xidmətləri.</summary>
    [Route("api/v1/services")]
    public class ServicesController : ResourceController
    {
        private readonly IServiceService _service;
        public ServicesController(IServiceService service) => _service = service;

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<ServiceVM>>> List() => await _service.GetAllAsync();

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<ActionResult<ServiceVM>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] ServiceVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] ServiceVM vm)
        {
            vm.Id = id;
            await _service.UpdateAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/technicians")]
    public class TechniciansController : ResourceController
    {
        private readonly ITechnicianService _service;
        public TechniciansController(ITechnicianService service) => _service = service;

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<Technician>>> List() => await _service.GetAllAsync();

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<ActionResult<Technician>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] TechnicianCreateVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] TechnicianEditVM vm)
        {
            vm.Id = id;
            await _service.EditAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}
