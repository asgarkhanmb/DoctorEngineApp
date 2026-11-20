using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class InformService :IInformService
    {
        private readonly AppDbContext _context;

        public InformService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Inform>> GetAllAsync()
        {
            return await _context.Informs.ToListAsync();
        }

        public async Task<Inform?> GetByIdAsync(int id)
        {
            return await _context.Informs.FindAsync(id);
        }

        public async Task CreateAsync(Inform inform)
        {
            await _context.Informs.AddAsync(inform);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Inform inform)
        {
            _context.Informs.Update(inform);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var data = await _context.Informs.FindAsync(id);
            if (data != null)
            {
                _context.Informs.Remove(data);
                await _context.SaveChangesAsync();
            }
        }
    }
}
