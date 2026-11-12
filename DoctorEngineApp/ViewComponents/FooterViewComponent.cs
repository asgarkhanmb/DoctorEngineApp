using DoctorEngineApp.Data;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.ViewComponents
{
    public class FooterViewComponent : ViewComponent
    {
        /// <summary>Footer-də göstərilən xidmət sayı — çox olsa da footer böyüməsin.</summary>
        public const int MaxServices = 5;

        private readonly IContactInfoService _contactInfoService;
        private readonly AppDbContext _db;

        public FooterViewComponent(IContactInfoService contactInfoService, AppDbContext db)
        {
            _contactInfoService = contactInfoService;
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var contactInfo = await _contactInfoService.GetFirstAsync() ?? new();

            // "Xidmətlər" səhifəsinin kateqoriyaları — ən son əlavə olunan 5-i
            contactInfo.Services = await _db.ServiceCategories
                .AsNoTracking()
                .OrderByDescending(c => c.Id)
                .Take(MaxServices)
                .Select(c => new ServiceVM { Id = c.Id, Name = c.Name })
                .ToListAsync();

            ViewData["ServiceTotal"] = await _db.ServiceCategories.CountAsync();

            return View(contactInfo);
        }
    }
}
