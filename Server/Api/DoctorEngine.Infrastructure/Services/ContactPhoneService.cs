using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.ContactPhones;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services.Implementations
{
    public class ContactPhoneService : IContactPhoneService
    {
        private readonly AppDbContext _context;

        public ContactPhoneService(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // ADMIN PANEL
        // =========================

        public async Task<List<ContactPhoneVM>> GetAllAsync()
        {
            return await _context.ContactPhones
                .OrderByDescending(x => x.Id)
                .Select(x => new ContactPhoneVM
                {
                    Id = x.Id,
                    Subtitle = x.Subtitle,
                    Title = x.Title,
                    Description = x.Description,
                    PhoneNumber = x.PhoneNumber
                })
                .ToListAsync();
        }

        public async Task<ContactPhoneVM?> GetByIdAsync(int id)
        {
            return await _context.ContactPhones
                .Where(x => x.Id == id)
                .Select(x => new ContactPhoneVM
                {
                    Id = x.Id,
                    Subtitle = x.Subtitle,
                    Title = x.Title,
                    Description = x.Description,
                    PhoneNumber = x.PhoneNumber
                })
                .FirstOrDefaultAsync();
        }

        public async Task CreateAsync(ContactPhoneVM model)
        {
            ContactPhone entity = new ContactPhone
            {
                Subtitle = model.Subtitle,
                Title = model.Title,
                Description = model.Description,
                PhoneNumber = model.PhoneNumber
            };

            _context.ContactPhones.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ContactPhoneVM model)
        {
            var entity = await _context.ContactPhones
                .FirstOrDefaultAsync(x => x.Id == model.Id);

            if (entity == null)
                return;

            entity.Subtitle = model.Subtitle;
            entity.Title = model.Title;
            entity.Description = model.Description;
            entity.PhoneNumber = model.PhoneNumber;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.ContactPhones
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
                return;

            _context.ContactPhones.Remove(entity);
            await _context.SaveChangesAsync();
        }

        // =========================
        // SITE (CALL TO ACTION)
        // =========================

        public async Task<ContactPhoneVM?> GetActiveAsync()
        {
            // IsActive olmadığı üçün sadəcə ilk olanı götürürük
            return await _context.ContactPhones
                .OrderByDescending(x => x.Id)
                .Select(x => new ContactPhoneVM
                {
                    Id = x.Id,
                    Subtitle = x.Subtitle,
                    Title = x.Title,
                    Description = x.Description,
                    PhoneNumber = x.PhoneNumber
                })
                .FirstOrDefaultAsync();
        }
    }
}
