using DoctorEngineApp.ViewModels.Services;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Categories;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    /// <summary>
    /// Kateqoriyalar. Qayda: hər kateqoriyanın BİR videosu olur (SubService).
    /// Video kateqoriyanın öz formasından əlavə olunur / dəyişdirilir / silinir.
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _ctx;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(AppDbContext ctx, IWebHostEnvironment env, ILogger<CategoryService> logger)
        {
            _ctx = ctx;
            _env = env;
            _logger = logger;
        }

        private string IconFolder => Path.Combine(_env.WebRootPath, "uploads", "categories");
        private string VideoFolder => Path.Combine(_env.WebRootPath, "uploads", "subvideos");

        // ================= OXU =================

        public Task<int> CountAsync() => _ctx.ServiceCategories.CountAsync();

        public async Task<List<CategoryVM>> GetAllAsync()
        {
            return await _ctx.ServiceCategories
                .OrderBy(x => x.Id)
                .Select(x => new CategoryVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    Icon = x.Icon,
                    SubServices = x.SubServices.OrderBy(s => s.Id)
                        .Select(s => new SubServiceVM { Id = s.Id, Title = s.Title, Description = "", Video = s.Video })
                        .ToList()
                })
                .ToListAsync();
        }

        public async Task<CategoryEditVM?> GetForEditAsync(int id)
        {
            var e = await _ctx.ServiceCategories
                .Include(c => c.SubServices)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (e == null) return null;

            var subs = e.SubServices.OrderBy(s => s.Id).ToList();
            var primary = subs.FirstOrDefault();

            var vm = new CategoryEditVM
            {
                Id = e.Id,
                Name = e.Name,
                ExistingIcon = e.Icon,
                VideoId = primary?.Id,
                VideoTitle = primary?.Title,
                VideoDescription = primary?.Description,
                ExistingVideo = primary?.Video,
                ExtraVideos = subs.Skip(1).Select(s => new ExtraVideoVM { Id = s.Id, Title = s.Title, Video = s.Video }).ToList()
            };

            await FillEmptyCategoriesAsync(vm);
            return vm;
        }

        /// <summary>Formanı yenidən göstərmək üçün (xəta olanda) köməkçi siyahıları doldurur.</summary>
        public async Task FillEmptyCategoriesAsync(CategoryEditVM vm)
        {
            vm.EmptyCategories = await _ctx.ServiceCategories
                .Where(c => c.Id != vm.Id && !c.SubServices.Any())
                .OrderBy(c => c.Id)
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToListAsync();

            if (!vm.ExtraVideos.Any())
            {
                vm.ExtraVideos = await _ctx.SubServices
                    .Where(s => s.CategoryId == vm.Id)
                    .OrderBy(s => s.Id)
                    .Skip(1)
                    .Select(s => new ExtraVideoVM { Id = s.Id, Title = s.Title, Video = s.Video })
                    .ToListAsync();
            }
        }

        // ================= YAZ =================

        public async Task CreateAsync(CategoryCreateVM vm)
        {
            var entity = new ServiceCategory
            {
                Name = vm.Name.Trim(),
                Icon = await SaveIconAsync(vm.IconFile, vm.PresetIcon) ?? ""
            };

            if (vm.VideoFile != null)
            {
                entity.SubServices.Add(new SubService
                {
                    Title = vm.VideoTitle!.Trim(),
                    Description = vm.VideoDescription!.Trim(),
                    Video = await SaveFileAsync(vm.VideoFile, VideoFolder)
                });
            }

            _ctx.ServiceCategories.Add(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(CategoryEditVM vm)
        {
            var entity = await _ctx.ServiceCategories
                .Include(c => c.SubServices)
                .FirstOrDefaultAsync(c => c.Id == vm.Id)
                ?? throw new InvalidOperationException("Kateqoriya tapılmadı");

            var oldFiles = new List<string>();

            // --- Ad və ikon ---
            entity.Name = vm.Name.Trim();
            var newIcon = await SaveIconAsync(vm.IconFile, vm.PresetIcon);
            if (newIcon != null)
            {
                if (!string.IsNullOrEmpty(entity.Icon)) oldFiles.Add(Path.Combine(IconFolder, entity.Icon));
                entity.Icon = newIcon;
            }

            // --- Video (əsas = ən köhnə qeyd) ---
            var primary = entity.SubServices.OrderBy(s => s.Id).FirstOrDefault();

            if (primary != null && vm.RemoveVideo)
            {
                if (!string.IsNullOrEmpty(primary.Video)) oldFiles.Add(Path.Combine(VideoFolder, primary.Video));
                _ctx.SubServices.Remove(primary);
            }
            else if (primary != null)
            {
                primary.Title = (vm.VideoTitle ?? primary.Title).Trim();
                primary.Description = (vm.VideoDescription ?? primary.Description).Trim();
                if (vm.VideoFile != null)
                {
                    if (!string.IsNullOrEmpty(primary.Video)) oldFiles.Add(Path.Combine(VideoFolder, primary.Video));
                    primary.Video = await SaveFileAsync(vm.VideoFile, VideoFolder);
                }
            }
            else if (vm.VideoFile != null)
            {
                entity.SubServices.Add(new SubService
                {
                    Title = vm.VideoTitle!.Trim(),
                    Description = vm.VideoDescription!.Trim(),
                    Video = await SaveFileAsync(vm.VideoFile, VideoFolder)
                });
            }

            await _ctx.SaveChangesAsync();

            // Köhnə fayllar yalnız baza uğurla yeniləndikdən sonra silinir
            oldFiles.ForEach(DeleteFile);
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _ctx.ServiceCategories.Include(c => c.SubServices).FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return;

            var files = entity.SubServices
                .Where(s => !string.IsNullOrEmpty(s.Video))
                .Select(s => Path.Combine(VideoFolder, s.Video))
                .ToList();
            if (!string.IsNullOrEmpty(entity.Icon)) files.Add(Path.Combine(IconFolder, entity.Icon));

            _ctx.ServiceCategories.Remove(entity);
            await _ctx.SaveChangesAsync();

            files.ForEach(DeleteFile);
        }

        // ================= FAYLLAR =================

        /// <summary>
        /// Yüklənmiş fayl varsa onu, yoxsa seçilmiş hazır ikonu uploads/categories-ə saxlayır.
        /// Hazır ikon kopyalanır ki, kateqoriya silinəndə/dəyişəndə orijinal toxunulmaz qalsın.
        /// Heç biri yoxdursa null qaytarır.
        /// </summary>
        private async Task<string?> SaveIconAsync(IFormFile? file, string? preset)
        {
            if (file != null) return await SaveFileAsync(file, IconFolder);
            if (!CategoryIconPresets.IsValid(preset)) return null;

            var source = Path.Combine(_env.WebRootPath, CategoryIconPresets.WebFolder, preset + ".png");
            if (!File.Exists(source))
            {
                _logger.LogWarning("Hazır ikon faylı tapılmadı: {Path}", source);
                return null;
            }

            Directory.CreateDirectory(IconFolder);
            var name = Guid.NewGuid() + ".png";
            File.Copy(source, Path.Combine(IconFolder, name));
            return name;
        }

        private static async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
            await file.CopyToAsync(stream);
            return name;
        }

        private void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fayl silinə bilmədi: {Path}", path);
            }
        }
    }
}
