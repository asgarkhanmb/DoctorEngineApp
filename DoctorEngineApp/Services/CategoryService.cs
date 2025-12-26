using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Categories;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class CategoryService :ICategoryService
    {
        private readonly AppDbContext _ctx;
        private readonly IWebHostEnvironment _env;
        public CategoryService(AppDbContext ctx, IWebHostEnvironment env) { _ctx = ctx; _env = env; }

        public async Task<List<CategoryVM>> GetAllAsync()
        {
            return await _ctx.ServiceCategories
                .Select(x => new CategoryVM { Id = x.Id, Name = x.Name, Icon = x.Icon })
                .ToListAsync();
        }

        public async Task<CategoryEditVM?> GetForEditAsync(int id)
        {
            var e = await _ctx.ServiceCategories.FindAsync(id);
            if (e == null) return null;
            return new CategoryEditVM { Id = e.Id, Name = e.Name, ExistingIcon = e.Icon };
        }

        public async Task CreateAsync(CategoryCreateVM vm)
        {
            string fileName = "";
            if (vm.IconFile != null)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "categories");
                Directory.CreateDirectory(folder);
                fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.IconFile.FileName);
                var fullPath = Path.Combine(folder, fileName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await vm.IconFile.CopyToAsync(stream);
            }

            var entity = new ServiceCategory { Name = vm.Name, Icon = fileName };
            _ctx.ServiceCategories.Add(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task UpdateAsync(CategoryEditVM vm)
        {
            var entity = await _ctx.ServiceCategories.FindAsync(vm.Id);
            if (entity == null) throw new Exception("Category not found");

            if (vm.IconFile != null)
            {
                // save new
                var folder = Path.Combine(_env.WebRootPath, "uploads", "categories");
                Directory.CreateDirectory(folder);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.IconFile.FileName);
                var fullPath = Path.Combine(folder, fileName);
                using var stream = new FileStream(fullPath, FileMode.Create);
                await vm.IconFile.CopyToAsync(stream);

                // delete old if exists
                if (!string.IsNullOrEmpty(entity.Icon))
                {
                    var old = Path.Combine(folder, entity.Icon);
                    if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                }
                entity.Icon = fileName;
            }

            entity.Name = vm.Name;
            _ctx.ServiceCategories.Update(entity);
            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _ctx.ServiceCategories.Include(c => c.SubServices).FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return;

            // delete related subservice videos from disk
            var videoFolder = Path.Combine(_env.WebRootPath, "uploads", "subvideos");
            foreach (var s in entity.SubServices)
            {
                if (!string.IsNullOrEmpty(s.Video))
                {
                    var vpath = Path.Combine(videoFolder, s.Video);
                    if (System.IO.File.Exists(vpath)) System.IO.File.Delete(vpath);
                }
            }

            // delete icon
            var iconFolder = Path.Combine(_env.WebRootPath, "uploads", "categories");
            if (!string.IsNullOrEmpty(entity.Icon))
            {
                var ipath = Path.Combine(iconFolder, entity.Icon);
                if (System.IO.File.Exists(ipath)) System.IO.File.Delete(ipath);
            }

            _ctx.ServiceCategories.Remove(entity);
            await _ctx.SaveChangesAsync();
        }
    }
}
