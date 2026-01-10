using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Controllers
{
    public class ServiceController : Controller
    {
        private readonly IServiceService _serviceService;
        private readonly ICategoryService _categoryService;
        private readonly AppDbContext _db;

        public ServiceController(IServiceService serviceService, ICategoryService categoryService, AppDbContext db)
        {
            _serviceService = serviceService;
            _categoryService = categoryService;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var services = await _serviceService.GetAllAsync();
            var categories = await _db.ServiceCategories
                .Include(c => c.SubServices)
                .Select(c => new CategoryVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    Icon = c.Icon,
                    SubServices = c.SubServices.Select(s => new SubServiceVM
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Description = s.Description,
                        Video = s.Video
                    }).ToList()
                }).ToListAsync();

            var model = new ServicePageVM
            {
                Services = services,
                Categories = categories
            };

            return View(model);
        }
    }
}
