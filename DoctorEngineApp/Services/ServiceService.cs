using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class ServiceService : IServiceService
    {
        private readonly AppDbContext _context;

        public ServiceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ServiceVM>> GetAllAsync()
        {
            return await _context.Services
                .Select(s => new ServiceVM
                {
                    Id = s.Id,
                    Name = s.Name,
                    WhatsApp = s.WhatsApp
                }).ToListAsync();
        }

        public async Task<ServiceVM> GetByIdAsync(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return null;

            return new ServiceVM
            {
                Id = service.Id,
                Name = service.Name,
                WhatsApp = service.WhatsApp
            };
        }

        public async Task CreateAsync(ServiceVM model)
        {
            var service = new Service
            {
                Name = model.Name,
                WhatsApp = model.WhatsApp
            };

            await _context.Services.AddAsync(service);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ServiceVM model)
        {
            var service = await _context.Services.FindAsync(model.Id);
            if (service == null) throw new InvalidOperationException("Service not found");

            service.Name = model.Name;
            service.WhatsApp = model.WhatsApp;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
            }
        }
    }

}
