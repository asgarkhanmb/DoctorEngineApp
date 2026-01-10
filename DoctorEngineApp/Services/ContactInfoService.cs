using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.ContactInfos;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class ContactInfoService :IContactInfoService
    {
        private readonly AppDbContext _context;

        public ContactInfoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ContactInfoVm> GetAsync(int id)
        {
            var entity = await _context.ContactInfos.FindAsync(id);
            if (entity == null) return null;

            return new ContactInfoVm
            {
                Id = entity.Id,
                Address = entity.Address,
                Phone = entity.Phone,
                Email = entity.Email,
                YouTube = entity.YouTube,
                Facebook = entity.Facebook,
                TikTok = entity.TikTok,
                WhatsApp = entity.WhatsApp,
                Weekdays = entity.Weekdays,
                Weekend = entity.Weekend
            };
        }

        public async Task<ContactInfoVm> GetFirstAsync()
        {
            var entity = await _context.ContactInfos.FirstOrDefaultAsync();
            if (entity == null) return null;

            return new ContactInfoVm
            {
                Id = entity.Id,
                Address = entity.Address,
                Phone = entity.Phone,
                Email = entity.Email,
                YouTube = entity.YouTube,
                Facebook = entity.Facebook,
                TikTok = entity.TikTok,
                WhatsApp = entity.WhatsApp,
                Weekdays = entity.Weekdays,
                Weekend = entity.Weekend
            };
        }

        public async Task CreateAsync(ContactInfoVm model)
        {
            var entity = new ContactInfo
            {
                Address = model.Address,
                Phone = model.Phone,
                Email = model.Email,
                YouTube = model.YouTube,
                Facebook = model.Facebook,
                TikTok = model.TikTok,
                WhatsApp = model.WhatsApp,
                Weekdays = model.Weekdays,
                Weekend = model.Weekend
            };

            _context.ContactInfos.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ContactInfoVm model)
        {
            var entity = await _context.ContactInfos.FindAsync(model.Id);
            if (entity == null) throw new Exception("Contact not found");

            entity.Address = model.Address;
            entity.Phone = model.Phone;
            entity.Email = model.Email;
            entity.YouTube = model.YouTube;
            entity.Facebook = model.Facebook;
            entity.TikTok = model.TikTok;
            entity.WhatsApp = model.WhatsApp;
            entity.Weekdays = model.Weekdays;
            entity.Weekend = model.Weekend;

            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync()
        {
            return await _context.ContactInfos.AnyAsync();
        }
    }
}
