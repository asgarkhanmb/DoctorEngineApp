using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class SubServiceService : ISubServiceService
    {
        private readonly AppDbContext _ctx;
        private readonly IWebHostEnvironment _env;
        public SubServiceService(AppDbContext ctx, IWebHostEnvironment env) { _ctx = ctx; _env = env; }

        public async Task<List<SubServiceVM>> GetAllAsync()
        {
            var list = await _ctx.SubServices
                .Include(s => s.Category)
                .OrderBy(s => s.CategoryId).ThenBy(s => s.Id)
                .Select(x => new SubServiceVM
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Video = x.Video,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category != null ? x.Category.Name : ""
                }).ToListAsync();

            // Hər kateqoriyada yalnız birinci video saytda göstərilir — qalanlarını işarələ
            foreach (var group in list.GroupBy(s => s.CategoryId))
                foreach (var extra in group.Skip(1))
                    extra.IsExtra = true;

            return list;
        }

        public async Task<SubServiceEditVM?> GetForEditAsync(int id)
        {
            var e = await _ctx.SubServices.FindAsync(id);
            if (e == null) return null;
            return new SubServiceEditVM
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                ExistingVideo = e.Video,
                CategoryId = e.CategoryId
            };
        }

        public async Task CreateAsync(SubServiceCreateVM vm)
        {
            string videoFile = "";
            if (vm.VideoFile != null)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "subvideos");
                Directory.CreateDirectory(folder);
                videoFile = Guid.NewGuid().ToString() + Path.GetExtension(vm.VideoFile.FileName);
                var fullPath = Path.Combine(folder, videoFile);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await vm.VideoFile.CopyToAsync(stream);
            }

            var entity = new SubService
            {
                Title = vm.Title,
                Description = vm.Description,
                Video = videoFile,
                CategoryId = vm.CategoryId
            };

            _ctx.SubServices.Add(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(SubServiceEditVM vm)
        {
            var entity = await _ctx.SubServices.FindAsync(vm.Id);
            if (entity == null) throw new Exception("Video tapılmadı — artıq silinmiş ola bilər.");

            if (vm.VideoFile != null)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "subvideos");
                Directory.CreateDirectory(folder);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.VideoFile.FileName);
                var fullPath = Path.Combine(folder, fileName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await vm.VideoFile.CopyToAsync(stream);

                // delete old
                if (!string.IsNullOrEmpty(entity.Video))
                {
                    var old = Path.Combine(folder, entity.Video);
                    if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                }
                entity.Video = fileName;
            }

            entity.Title = vm.Title;
            entity.Description = vm.Description;
            entity.CategoryId = vm.CategoryId;

            _ctx.SubServices.Update(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _ctx.SubServices.FindAsync(id);
            if (entity == null) return;

            if (!string.IsNullOrEmpty(entity.Video))
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "subvideos");
                var p = Path.Combine(folder, entity.Video);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }

            _ctx.SubServices.Remove(entity);
            await _ctx.SaveChangesAsync();
        }
    }
}
