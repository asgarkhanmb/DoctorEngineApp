using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.ContactMessage;
using Microsoft.EntityFrameworkCore;

public class ContactMessageService : IContactMessageService
{
    private readonly AppDbContext _context;

    public ContactMessageService(AppDbContext context)
    {
        _context = context;
    }

    // CONTACT PAGE
    public async Task CreateAsync(ContactMessageCreateVM model)
    {
        try
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);

            bool alreadySent = await _context.ContactMessages.AnyAsync(x =>
                x.Email == model.Email &&
                x.CreatedDate >= oneHourAgo);

            if (alreadySent)
                throw new Exception("Bu email ilə son 1 saat ərzində artıq mesaj göndərilib.");

            var entity = new ContactMessage
            {
                FullName = model.FullName,
                Email = model.Email,
                Subject = model.Subject,
                Message = model.Message,
                CreatedDate = DateTime.UtcNow
            };

            await _context.ContactMessages.AddAsync(entity);
            await _context.SaveChangesAsync();
        }
        catch
        {
            throw; // exception controller-ə ötürülsün
        }
    }


    // ADMIN
    public async Task<List<ContactMessage>> GetAllAsync()
    {
        return await _context.ContactMessages
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();
    }

    public async Task<ContactMessage?> GetByIdAsync(int id)
    {
        return await _context.ContactMessages
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task DeleteAsync(int id)
    {
        var message = await _context.ContactMessages.FindAsync(id);

        if (message == null)
            throw new Exception("Mesaj tapılmadı — artıq silinmiş ola bilər.");

        _context.ContactMessages.Remove(message);
        await _context.SaveChangesAsync();
    }
}
