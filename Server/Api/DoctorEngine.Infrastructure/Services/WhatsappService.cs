using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;


public class WhatsappService : IWhatsappService
{
    private readonly AppDbContext _context;

    public WhatsappService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WhatsappSetting> GetAsync()
    {
        return await _context.WhatsappSettings.FirstOrDefaultAsync();
    }

    public async Task CreateAsync(WhatsappSetting model)
    {
        await _context.WhatsappSettings.AddAsync(model);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(WhatsappSetting model)
    {
        _context.WhatsappSettings.Update(model);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var data = await _context.WhatsappSettings.FindAsync(id);

        if (data != null)
        {
            _context.WhatsappSettings.Remove(data);
            await _context.SaveChangesAsync();
        }
    }
}