using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DoctorEngineApp.Services
{
    public class SiteSettingService : ISiteSettingService
    {
        private const string CacheKey = "site:settings";

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SiteSettingService> _logger;

        public SiteSettingService(AppDbContext db, IMemoryCache cache, IWebHostEnvironment env, ILogger<SiteSettingService> logger)
        {
            _db = db;
            _cache = cache;
            _env = env;
            _logger = logger;
        }

        private string Folder => Path.Combine(_env.WebRootPath, "uploads", "site");

        public async Task<SiteSetting> GetAsync()
        {
            // Hər səhifədə (header, footer, başlıq) istifadə olunur — bazaya hər dəfə getməsin
            return (await _cache.GetOrCreateAsync(CacheKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                var s = await _db.SiteSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
                return s ?? new SiteSetting();
            }))!;
        }

        public async Task SaveAsync(SiteSettingVM vm)
        {
            var entity = await _db.SiteSettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (entity == null)
            {
                entity = new SiteSetting();
                _db.SiteSettings.Add(entity);
            }

            entity.SiteName = vm.SiteName.Trim();
            entity.LogoIcon = SiteDefaults.IsAllowedIcon(vm.LogoIcon) ? vm.LogoIcon : SiteDefaults.Icon;

            string? oldFile = null;
            if (vm.LogoFile != null)
            {
                Directory.CreateDirectory(Folder);
                var name = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.LogoFile.FileName).ToLowerInvariant();
                await using (var fs = new FileStream(Path.Combine(Folder, name), FileMode.CreateNew))
                    await vm.LogoFile.CopyToAsync(fs);

                oldFile = entity.LogoImage;
                entity.LogoImage = name;
            }
            else if (vm.RemoveLogo)
            {
                oldFile = entity.LogoImage;
                entity.LogoImage = null;
            }

            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey);

            if (!string.IsNullOrEmpty(oldFile))
            {
                try
                {
                    var path = Path.Combine(Folder, Path.GetFileName(oldFile));
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Köhnə loqo silinə bilmədi: {File}", oldFile);
                }
            }
        }
    }
}
