using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Warranties;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services.Implementations
{
    public class WarrantyService : IWarrantyService
    {
        private readonly AppDbContext _context;

        public WarrantyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Warranty>> GetAllAsync()
        {
            return await _context.Warranties
                .Include(w => w.Items)
                .OrderByDescending(w => w.CreatedDate)
                .ToListAsync();
        }

        public async Task<Warranty> GetByIdAsync(int id)
        {
            return await _context.Warranties
                .Include(x => x.Items)   // 🔥 mütləq
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Warranty> CreateAsync(WarrantyCreateVM model)
        {
            var warranty = new Warranty
            {
                CustomerFullName = model.CustomerFullName,
                VehicleModel = model.VehicleModel,
                VehicleYear = model.VehicleYear,
                Terms = model.Terms,
                CreatedDate = DateTime.Now,
                Items = model.Items.Select(x => new WarrantyItem
                {
                    PartName = x.PartName,
                    WarrantyMonth = x.WarrantyMonth
                }).ToList()
            };

            _context.Warranties.Add(warranty);
            await _context.SaveChangesAsync();
            return warranty;
        }

        public async Task DeleteAsync(int id)
        {
            var warranty = await _context.Warranties
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (warranty == null) return;

            _context.WarrantyItems.RemoveRange(warranty.Items);
            _context.Warranties.Remove(warranty);
            await _context.SaveChangesAsync();
        }
    }
}