using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels;
using DoctorEngineApp.ViewModels.Technicians;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class TechnicianService : ITechnicianService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TechnicianService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ================================
        // GET ALL
        // ================================
        public async Task<List<Technician>> GetAllAsync()
        {
            return await _context.Technicians
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.Id)
                .ToListAsync();
        }

        // ================================
        // GET BY ID
        // ================================
        public async Task<Technician> GetByIdAsync(int id)
        {
            return await _context.Technicians
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        // ================================
        // CREATE
        // ================================
        public async Task CreateAsync(TechnicianCreateVM vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            string fileName = null;

            if (vm.Photo != null)
            {
                ValidateImage(vm.Photo);

                string folderPath = Path.Combine(
                    _env.WebRootPath, "uploads", "technicians");

                // Qovluq yoxdursa yarat
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                fileName = Guid.NewGuid() + Path.GetExtension(vm.Photo.FileName);
                string filePath = Path.Combine(folderPath, fileName);

                using FileStream stream = new(filePath, FileMode.Create);
                await vm.Photo.CopyToAsync(stream);
            }

            Technician technician = new()
            {
                FullName = vm.FullName,
                Position = vm.Position,
                Image = fileName,
                FacebookUrl = vm.FacebookUrl,
                InstagramUrl = vm.InstagramUrl,
                TiktokUrl = vm.TiktokUrl,
                IsDeleted = false
            };

            await _context.Technicians.AddAsync(technician);
            await _context.SaveChangesAsync();
        }

        // ================================
        // EDIT
        // ================================
        public async Task EditAsync(TechnicianEditVM vm)
        {
            var tech = await GetByIdAsync(vm.Id);
            if (tech == null)
                throw new InvalidOperationException("Texnik tapılmadı");

            // Yeni şəkil varsa
            if (vm.Photo != null)
            {
                ValidateImage(vm.Photo);

                string folderPath = Path.Combine(
                    _env.WebRootPath, "uploads", "technicians");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                // Köhnə şəkli sil
                if (!string.IsNullOrEmpty(tech.Image))
                {
                    string oldPath = Path.Combine(folderPath, tech.Image);
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }

                string fileName = Guid.NewGuid() + Path.GetExtension(vm.Photo.FileName);
                string filePath = Path.Combine(folderPath, fileName);

                using FileStream stream = new(filePath, FileMode.Create);
                await vm.Photo.CopyToAsync(stream);

                tech.Image = fileName;
            }

            tech.FullName = vm.FullName;
            tech.Position = vm.Position;
            tech.FacebookUrl = vm.FacebookUrl;
            tech.InstagramUrl = vm.InstagramUrl;
            tech.TiktokUrl = vm.TiktokUrl;

            _context.Update(tech);
            await _context.SaveChangesAsync();
        }

        // ================================
        // DELETE (SOFT + IMAGE DELETE)
        // ================================
        public async Task DeleteAsync(int id)
        {
            var tech = await GetByIdAsync(id);
            if (tech == null)
                throw new InvalidOperationException("Texnik tapılmadı");

            // Şəkli sil
            if (!string.IsNullOrEmpty(tech.Image))
            {
                string folderPath = Path.Combine(
                    _env.WebRootPath, "uploads", "technicians");

                string imagePath = Path.Combine(folderPath, tech.Image);

                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }

            tech.IsDeleted = true;
            await _context.SaveChangesAsync();
        }
        private void ValidateImage(IFormFile file)
        {
            var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLower();

            if (!allowedTypes.Contains(ext))
                throw new InvalidOperationException("Şəkil formatı düzgün deyil");

            const long maxSize = 1000 * 1024 * 1024; 

            if (file.Length > maxSize)
                throw new InvalidOperationException("Şəkil ölçüsü maksimum 1000MB ola bilər");
        }
    }
}
