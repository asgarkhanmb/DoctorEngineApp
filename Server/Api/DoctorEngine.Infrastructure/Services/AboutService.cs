using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Abouts;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class AboutService :IAboutService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private readonly string[] _allowedExtensions = new[]
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv"
        };

        private const long MaxVideoBytes = 1L * 1024 * 1024 * 1024; // 1GB

        public AboutService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<List<About>> GetAllAsync()
        {
            return await _context.Abouts
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();
        }

        public async Task<About?> GetByIdAsync(int id)
        {
            return await _context.Abouts.FindAsync(id);
        }

        // Optionally get the first/single About record
        public async Task<About?> GetSingleAsync()
        {
            return await _context.Abouts.OrderByDescending(a => a.CreatedDate).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(AboutCreateVM vm)
        {
            if (vm.VideoFile == null)
                throw new Exception("Video faylı qoyulmayıb.");

            var ext = Path.GetExtension(vm.VideoFile.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(ext))
                throw new Exception("Dəstəklənməyən video formatı.");

            if (vm.VideoFile.Length > MaxVideoBytes)
                throw new Exception("Video maksimum 1GB ola bilər (təxminən 40 dəq).");

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "about");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string fileName = $"{Guid.NewGuid()}{ext}";
            string filePath = Path.Combine(uploadsFolder, fileName);

            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await vm.VideoFile.CopyToAsync(fs);
            }

            var about = new About
            {
                Title = vm.Title,
                Description = vm.Description,
                VideoPath = $"/uploads/about/{fileName}",
                CreatedDate = DateTime.UtcNow
            };

            _context.Abouts.Add(about);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(AboutEditVM vm)
        {
            var about = await _context.Abouts.FindAsync(vm.Id);
            if (about == null)
                throw new Exception("Haqqımızda məlumatı tapılmadı.");

            about.Title = vm.Title;
            about.Description = vm.Description;

            if (vm.VideoFile != null)
            {
                var ext = Path.GetExtension(vm.VideoFile.FileName).ToLowerInvariant();
                if (!_allowedExtensions.Contains(ext))
                    throw new Exception("Dəstəklənməyən video formatı.");

                if (vm.VideoFile.Length > MaxVideoBytes)
                    throw new Exception("Video maksimum 1GB ola bilər (təxminən 40 dəq).");

                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "about");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string newFileName = $"{Guid.NewGuid()}{ext}";
                string newPath = Path.Combine(uploadsFolder, newFileName);

                using (var fs = new FileStream(newPath, FileMode.Create))
                {
                    await vm.VideoFile.CopyToAsync(fs);
                }

                // Delete old file if exists
                if (!string.IsNullOrEmpty(about.VideoPath))
                {
                    string oldFull = Path.Combine(_env.WebRootPath, about.VideoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(oldFull))
                        File.Delete(oldFull);
                }

                about.VideoPath = $"/uploads/about/{newFileName}";
            }

            about.CreatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var about = await _context.Abouts.FindAsync(id);
            if (about == null)
                throw new Exception("Haqqımızda məlumatı tapılmadı.");

            if (!string.IsNullOrEmpty(about.VideoPath))
            {
                string full = Path.Combine(_env.WebRootPath, about.VideoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                    File.Delete(full);
            }

            _context.Abouts.Remove(about);
            await _context.SaveChangesAsync();
        }
    }
}
