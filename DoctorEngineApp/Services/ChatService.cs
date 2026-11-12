using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Chats;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class ChatService : IChatService
    {
        public const int MaxMessageLength = 1000;
        public const long MaxImageBytes = 10L * 1024 * 1024;   // 10 MB
        public const long MaxVideoBytes = 100L * 1024 * 1024;  // 100 MB

        private const string UploadFolder = "uploads/chat";

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChatService> _logger;

        public ChatService(AppDbContext context, IWebHostEnvironment env, ILogger<ChatService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        // ================= OXU =================

        public async Task<ChatPageDto> GetPageAsync(int? beforeId, int take)
        {
            take = Math.Clamp(take, 1, 100);

            var query = _context.ChatMessages.AsNoTracking().Where(m => !m.SoftDeleted);
            if (beforeId.HasValue)
                query = query.Where(m => m.Id < beforeId.Value);

            // Ən yenilərdən take+1 götür (daha köhnə mesaj olub-olmadığını bilmək üçün)
            var items = await query
                .OrderByDescending(m => m.Id)
                .Take(take + 1)
                .ToListAsync();

            var hasMore = items.Count > take;
            var page = items.Take(take).Reverse().Select(ToDto).ToList();

            return new ChatPageDto { Messages = page, HasMore = hasMore };
        }

        // ================= GÖNDƏR =================

        public async Task<ChatResult> SendAsync(ChatSendVM vm, string username, bool isAdmin)
        {
            var text = Normalize(vm.Message);

            if (text == null && vm.File == null)
                return ChatResult.Invalid("Mesaj və ya fayl əlavə edin.");

            if (text != null && text.Length > MaxMessageLength)
                return ChatResult.Invalid($"Mesaj maksimum {MaxMessageLength} simvol ola bilər.");

            string? url = null, type = null;
            if (vm.File != null)
            {
                var saved = await SaveFileAsync(vm.File);
                if (saved.Error != null)
                    return ChatResult.Invalid(saved.Error);
                (url, type) = (saved.Url, saved.Type);
            }

            var msg = new ChatMessage
            {
                Message = text,
                UserName = username,
                SenderIsAdmin = isAdmin,
                AttachmentUrl = url,
                AttachmentType = type,
                CreatedDate = AzTime.Now
            };

            try
            {
                await _context.ChatMessages.AddAsync(msg);
                await _context.SaveChangesAsync();
            }
            catch
            {
                DeleteFile(url); // bazaya yazılmadısa faylı yetim qoyma
                throw;
            }

            return ChatResult.Ok(ToDto(msg));
        }

        // ================= REDAKTƏ =================

        public async Task<ChatResult> EditAsync(ChatEditVM vm, string username)
        {
            var msg = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == vm.Id && !m.SoftDeleted);

            // Başqasının mesajı da "tapılmadı" kimi qaytarılır
            if (msg == null || msg.UserName != username)
                return ChatResult.NotFound();

            var text = Normalize(vm.Message);
            if (text != null && text.Length > MaxMessageLength)
                return ChatResult.Invalid($"Mesaj maksimum {MaxMessageLength} simvol ola bilər.");

            var keepsAttachment = msg.AttachmentUrl != null && !vm.RemoveAttachment && vm.File == null;
            if (text == null && vm.File == null && !keepsAttachment)
                return ChatResult.Invalid("Mesaj boş qala bilməz. Mətn yazın və ya fayl əlavə edin.");

            string? oldFile = null;

            if (vm.File != null)
            {
                var saved = await SaveFileAsync(vm.File);
                if (saved.Error != null)
                    return ChatResult.Invalid(saved.Error);

                oldFile = msg.AttachmentUrl;
                msg.AttachmentUrl = saved.Url;
                msg.AttachmentType = saved.Type;
            }
            else if (vm.RemoveAttachment && msg.AttachmentUrl != null)
            {
                oldFile = msg.AttachmentUrl;
                msg.AttachmentUrl = null;
                msg.AttachmentType = null;
            }

            msg.Message = text;
            msg.IsEdited = true;
            msg.EditedDate = AzTime.Now;

            await _context.SaveChangesAsync();

            // Köhnə fayl yalnız baza uğurla yeniləndikdən sonra silinir
            DeleteFile(oldFile);

            return ChatResult.Ok(ToDto(msg));
        }

        // ================= SİL =================

        public async Task<bool> DeleteAsync(int id, string username, bool isAdmin)
        {
            var msg = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == id && !m.SoftDeleted);

            if (msg == null || (msg.UserName != username && !isAdmin))
                return false;

            var file = msg.AttachmentUrl;
            _context.ChatMessages.Remove(msg);
            await _context.SaveChangesAsync();

            DeleteFile(file);
            return true;
        }

        // ================= KÖMƏKÇİLƏR =================

        private static string? Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Sətir sonlarını vahid formaya sal, kənar boşluqları sil
            return text.Replace("\r\n", "\n").Trim();
        }

        private async Task<(string? Url, string? Type, string? Error)> SaveFileAsync(IFormFile file)
        {
            var kind = AllowedUploadAttribute.DetectKind(file);
            if (kind == null)
                return (null, null, "Yalnız şəkil (jpg, png, webp) və ya video (mp4, webm, mov) göndərilə bilər.");

            var max = kind == UploadKind.Image ? MaxImageBytes : MaxVideoBytes;
            if (!AllowedUploadAttribute.IsAllowed(file, kind.Value, out var error, max))
                return (null, null, error);

            var folder = Path.Combine(_env.WebRootPath, "uploads", "chat");
            Directory.CreateDirectory(folder);

            // Fayl adı istifadəçidən götürülmür — yalnız təsadüfi GUID + yoxlanmış uzantı
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            return ($"/{UploadFolder}/{fileName}", kind == UploadKind.Image ? "image" : "video", null);
        }

        private void DeleteFile(string? url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith($"/{UploadFolder}/"))
                return;

            try
            {
                var fileName = Path.GetFileName(url);
                var fullPath = Path.Combine(_env.WebRootPath, "uploads", "chat", fileName);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Çat faylı silinə bilmədi: {Url}", url);
            }
        }

        private static ChatMessageDto ToDto(ChatMessage m) => new()
        {
            Id = m.Id,
            UserName = m.UserName,
            Message = m.Message,
            IsAdmin = m.SenderIsAdmin,
            AttachmentUrl = m.AttachmentUrl,
            AttachmentType = m.AttachmentType,
            IsEdited = m.IsEdited,
            Date = m.CreatedDate.ToString("yyyy-MM-dd"),
            Time = m.CreatedDate.ToString("HH:mm")
        };
    }
}
