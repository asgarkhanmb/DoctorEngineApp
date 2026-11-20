using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Sliders;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class SliderService : ISliderService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SliderService> _logger;

        public SliderService(AppDbContext context, IWebHostEnvironment env, ILogger<SliderService> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        private string Folder => Path.Combine(_env.WebRootPath, "uploads", "sliders");

        // Fayl yükləmə (ad istifadəçidən götürülmür — GUID + yoxlanmış uzantı)
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            Directory.CreateDirectory(Folder);

            string filename = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using var fs = new FileStream(Path.Combine(Folder, filename), FileMode.CreateNew);
            await file.CopyToAsync(fs);

            return filename;
        }

        // Köhnə faylı sil (yalnız sliders qovluğunda, yol manipulyasiyası mümkün deyil)
        private void DeleteFile(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return;

            try
            {
                var path = Path.Combine(Folder, Path.GetFileName(filename));
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Slayder faylı silinə bilmədi: {File}", filename);
            }
        }

        // Saytda göstəriləcək slaydlar (silinməmiş, sabit sıra ilə)
        public async Task<List<Slider>> GetAllAsync()
        {
            return await _context.Sliders
                .AsNoTracking()
                .Where(s => !s.SoftDeleted)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        public async Task<Slider?> GetByIdAsync(int id)
        {
            return await _context.Sliders.FirstOrDefaultAsync(s => s.Id == id && !s.SoftDeleted);
        }

        public async Task CreateAsync(SliderVM vm)
        {
            string? img1 = vm.ImageFile1 != null ? await SaveFileAsync(vm.ImageFile1) : null;
            string? img2 = vm.ImageFile2 != null ? await SaveFileAsync(vm.ImageFile2) : null;

            _context.Sliders.Add(new Slider
            {
                Title = vm.Title.Trim(),
                Subtitle = vm.Subtitle.Trim(),
                Image1 = img1!,
                Image2 = img2!
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                DeleteFile(img1);
                DeleteFile(img2);
                throw;
            }
        }

        public async Task UpdateAsync(SliderVM vm)
        {
            var slider = await _context.Sliders.FirstOrDefaultAsync(s => s.Id == vm.Id && !s.SoftDeleted);
            if (slider == null) return;

            slider.Title = vm.Title.Trim();
            slider.Subtitle = vm.Subtitle.Trim();

            var oldFiles = new List<string?>();

            if (vm.ImageFile1 != null)
            {
                oldFiles.Add(slider.Image1);
                slider.Image1 = await SaveFileAsync(vm.ImageFile1);
            }

            if (vm.ImageFile2 != null)
            {
                oldFiles.Add(slider.Image2);
                slider.Image2 = await SaveFileAsync(vm.ImageFile2);
            }
            else if (vm.RemoveImage2 && !string.IsNullOrEmpty(slider.Image2))
            {
                oldFiles.Add(slider.Image2);
                slider.Image2 = null!;
            }

            await _context.SaveChangesAsync();

            // Köhnə fayllar yalnız baza uğurla yeniləndikdən sonra silinir
            oldFiles.ForEach(DeleteFile);
        }

        public async Task DeleteAsync(int id)
        {
            var slider = await _context.Sliders.FindAsync(id);
            if (slider == null) return;

            var files = new[] { slider.Image1, slider.Image2 };

            _context.Sliders.Remove(slider);
            await _context.SaveChangesAsync();

            foreach (var f in files) DeleteFile(f);
        }
    }
}
