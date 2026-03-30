using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class ChatService:IChatService
    {
        private readonly AppDbContext _context;

        public ChatService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ChatMessage>> GetAllAsync()
        {
            return await _context.ChatMessages
                .OrderBy(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<ChatMessage> AddAsync(string message, string username)
        {
            var msg = new ChatMessage
            {
                Message = message,
                UserName = username
            };

            await _context.ChatMessages.AddAsync(msg);
            await _context.SaveChangesAsync();

            return msg;
        }
        public async Task DeleteAsync(int id, string username)
        {
            var msg = await _context.ChatMessages.FindAsync(id);

            if (msg == null || msg.UserName != username)
                throw new Exception("İcazə yoxdur");

            _context.ChatMessages.Remove(msg);
            await _context.SaveChangesAsync();
        }

        public async Task<ChatMessage> UpdateAsync(int id, string message, string username)
        {
            var msg = await _context.ChatMessages.FindAsync(id);

            if (msg == null || msg.UserName != username)
                throw new Exception("İcazə yoxdur");

            msg.Message = message;
            msg.IsEdited = true;

            await _context.SaveChangesAsync();

            return msg;
        }
    }
}
