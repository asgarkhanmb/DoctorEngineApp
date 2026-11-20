using System.Text.Json;
using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Abouts;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Infroms;
using DoctorEngineApp.ViewModels.Services;
using DoctorEngineApp.ViewModels.Sliders;
using DoctorEngineApp.ViewModels.Technicians;
using DoctorEngineApp.ViewModels.Warranties;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.V1.AdminSections
{
    // =====================================================================
    // Saytın məzmun bölmələri. Qaydalar Areas/Admin/Controllers-dəki ilə eynidir.
    // =====================================================================

    public sealed class SliderSection : AdminSection
    {
        public override string Key => "sliders";
        public override string Title => "Slayderlər";
        public override string? Subtitle => "Ana səhifənin yuxarısındakı bannerlər";
        public override string Group => "Ana səhifə";

        private static readonly AdminFieldDto[] Fields =
        {
            Text("Title", "Kiçik başlıq", required: true, max: 60),
            Text("Subtitle", "Əsas başlıq", required: true, max: 120),
            Image("ImageFile1", "Fon şəkli", required: true),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            new((await Get<ISliderService>(sp).GetAllAsync())
                .Select(s => new AdminItemDto(s.Id, s.Subtitle, s.Title, U("sliders", s.Image1), null)).ToList(), null);

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, new[] { ("Title", (string?)""), ("Subtitle", "") });
            var s = await Get<ISliderService>(sp).GetByIdAsync(id.Value);
            return s == null ? null : Form(s.Id, Fields,
                new[] { ("Title", (string?)s.Title), ("Subtitle", s.Subtitle) },
                new[] { ("ImageFile1", U("sliders", s.Image1)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var vm = new SliderVM();
            var error = await b.BindAsync(vm);
            if (id == null && vm.ImageFile1 == null) return "Fon şəkli məcburidir";
            if (error != null) return error;

            var service = Get<ISliderService>(sp);
            if (id == null) await service.CreateAsync(vm);
            else
            {
                if (await service.GetByIdAsync(id.Value) == null) return "Slayder tapılmadı.";
                vm.Id = id.Value;
                await service.UpdateAsync(vm);
            }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<ISliderService>(sp).DeleteAsync(id);
    }

    public sealed class InformSection : AdminSection
    {
        public override string Key => "informs";
        public override string Title => "Üstünlüklər";
        public override string? Subtitle => "“Niyə DoctorEngine” kartları";
        public override string Group => "Ana səhifə";

        private static readonly AdminFieldDto[] Fields =
        {
            Image("Icon", "İkon", required: true),
            Text("Title", "Başlıq", required: true, max: 80),
            Multi("Description", "Təsvir", required: true, max: 300),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp)
        {
            var list = await Get<AppDbContext>(sp).Informs.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
            return new(list.Select(i => new AdminItemDto(i.Id, i.Title, Short(i.Description), U("inform", i.Icon), null)).ToList(), null);
        }

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, new[] { ("Title", (string?)""), ("Description", "") });
            var i = await Get<AppDbContext>(sp).Informs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return i == null ? null : Form(i.Id, Fields,
                new[] { ("Title", (string?)i.Title), ("Description", i.Description) },
                new[] { ("Icon", U("inform", i.Icon)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            // Saytdakı InformController ilə eyni: fayl uploads/inform qovluğuna, köhnə ikon silinir
            var db = Get<AppDbContext>(sp);
            var env = Get<IWebHostEnvironment>(sp);

            if (id == null)
            {
                var vm = new InformCreateVM();
                var error = await b.BindAsync(vm);
                if (vm.Icon == null) return "İkon seçin";
                if (error != null) return error;

                db.Informs.Add(new Inform { Icon = await SaveFileAsync(env, vm.Icon), Title = vm.Title, Description = vm.Description });
            }
            else
            {
                var item = await db.Informs.FindAsync(id.Value);
                if (item == null) return "Qeyd tapılmadı.";
                var vm = new InformEditVM();
                var error = await b.BindAsync(vm);
                if (error != null) return error;

                if (vm.Icon != null)
                {
                    var old = item.Icon;
                    item.Icon = await SaveFileAsync(env, vm.Icon);
                    DeleteFile(env, old);
                }
                item.Title = vm.Title;
                item.Description = vm.Description;
            }
            await db.SaveChangesAsync();
            return null;
        }

        public override async Task DeleteAsync(IServiceProvider sp, int id)
        {
            var db = Get<AppDbContext>(sp);
            var item = await db.Informs.FindAsync(id);
            if (item == null) return;
            DeleteFile(Get<IWebHostEnvironment>(sp), item.Icon);
            db.Informs.Remove(item);
            await db.SaveChangesAsync();
        }

        private static async Task<string> SaveFileAsync(IWebHostEnvironment env, IFormFile file)
        {
            var folder = Path.Combine(env.WebRootPath, "uploads", "inform");
            Directory.CreateDirectory(folder);
            var name = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using (var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew))
                await file.CopyToAsync(stream);
            return "/uploads/inform/" + name;
        }

        private static void DeleteFile(IWebHostEnvironment env, string? webPath)
        {
            if (string.IsNullOrEmpty(webPath) || !webPath.StartsWith("/uploads/inform/")) return;
            var path = Path.Combine(env.WebRootPath, "uploads", "inform", Path.GetFileName(webPath));
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    public sealed class AboutSection : AdminSection
    {
        public override string Key => "about";
        public override string Title => "Haqqımızda";
        public override string? Subtitle => "Ana səhifə və Haqqımızda səhifəsindəki video blok";
        public override string Group => "Ana səhifə";

        private static readonly AdminFieldDto[] Fields =
        {
            Text("Title", "Başlıq", max: 200),
            Multi("Description", "Təsvir"),
            Video("VideoFile", "Video", required: true),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            new((await Get<IAboutService>(sp).GetAllAsync())
                .Select(a => new AdminItemDto(a.Id, string.IsNullOrWhiteSpace(a.Title) ? "Haqqımızda" : a.Title!, Short(a.Description), null,
                    string.IsNullOrEmpty(a.VideoPath) ? null : "Video")).ToList(), null);

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, new[] { ("Title", (string?)""), ("Description", "") });
            var a = await Get<IAboutService>(sp).GetByIdAsync(id.Value);
            return a == null ? null : Form(a.Id, Fields,
                new[] { ("Title", a.Title), ("Description", a.Description) },
                new[] { ("VideoFile", U("about", a.VideoPath)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var service = Get<IAboutService>(sp);
            if (id == null)
            {
                var vm = new AboutCreateVM();
                var error = await b.BindAsync(vm);
                if (vm.VideoFile == null) return "Video seçin.";
                if (error != null) return error;
                await service.CreateAsync(vm);
            }
            else
            {
                var current = await service.GetByIdAsync(id.Value);
                if (current == null) return "Haqqımızda məlumatı tapılmadı.";
                var vm = new AboutEditVM();
                var error = await b.BindAsync(vm);
                if (error != null) return error;
                vm.Id = id.Value;
                vm.CurrentVideo = current.VideoPath;
                await service.UpdateAsync(vm);
            }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<IAboutService>(sp).DeleteAsync(id);
    }

    public sealed class CategorySection : AdminSection
    {
        public override string Key => "categories";
        public override string Title => "Xidmət kateqoriyaları";
        public override string? Subtitle => $"Xidmətlər səhifəsindəki kartlar (maksimum {ServiceCategory.MaxCount})";
        public override string Group => "Xidmətlər";

        private static readonly AdminOptionDto[] Presets =
            CategoryIconPresets.All.Select(p => new AdminOptionDto(p.Key, p.Label)).ToArray();

        private static readonly AdminFieldDto[] Fields =
        {
            Text("Name", "Kateqoriyanın adı", required: true, max: 100),
            new("PresetIcon", "Hazır ikon", "select", Hint: "Hazır ikon seçin və ya aşağıda öz ikonunuzu yükləyin", Options: Presets),
            Image("IconFile", "Öz ikonunuz"),
            Text("VideoTitle", "Videonun başlığı", max: 150, hint: "Video yükləyirsinizsə məcburidir"),
            Multi("VideoDescription", "Videonun təsviri", max: 1000, hint: "Video yükləyirsinizsə məcburidir"),
            Video("VideoFile", "Video (ixtiyari)"),
            Bool("RemoveVideo", "Videonu sil", mode: "edit"),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp)
        {
            var service = Get<ICategoryService>(sp);
            var list = await service.GetAllAsync();
            var blocked = list.Count >= ServiceCategory.MaxCount
                ? $"Maksimum {ServiceCategory.MaxCount} kateqoriya ola bilər. Yenisini əlavə etmək üçün əvvəlcə birini silin."
                : null;
            return new(list.Select(c => new AdminItemDto(c.Id, c.Name,
                c.SubServices.FirstOrDefault()?.Title ?? "Video yoxdur", U("categories", c.Icon),
                c.SubServices.Count > 0 ? "Video" : null)).ToList(), blocked);
        }

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, new[] { ("Name", (string?)""), ("PresetIcon", ""), ("VideoTitle", ""), ("VideoDescription", "") });
            var c = await Get<ICategoryService>(sp).GetForEditAsync(id.Value);
            return c == null ? null : Form(c.Id, Fields,
                new[] { ("Name", (string?)c.Name), ("PresetIcon", ""), ("VideoTitle", c.VideoTitle), ("VideoDescription", c.VideoDescription) },
                new[] { ("IconFile", U("categories", c.ExistingIcon)), ("VideoFile", U("subvideos", c.ExistingVideo)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var service = Get<ICategoryService>(sp);
            if (id == null)
            {
                // Limit server tərəfdə də yoxlanılır
                if (await service.CountAsync() >= ServiceCategory.MaxCount)
                    return $"Maksimum {ServiceCategory.MaxCount} kateqoriya ola bilər. Yenisini əlavə etmək üçün əvvəlcə birini silin.";

                var vm = new CategoryCreateVM();
                var error = await b.BindAsync(vm);
                error = IconError(vm.IconFile != null, vm.PresetIcon, required: true)
                        ?? VideoError(vm.VideoFile != null, false, false, vm.VideoTitle, vm.VideoDescription)
                        ?? error;
                if (error != null) return error;
                await service.CreateAsync(vm);
            }
            else
            {
                var current = await service.GetForEditAsync(id.Value);
                if (current == null) return "Kateqoriya tapılmadı.";
                var vm = new CategoryEditVM();
                var error = await b.BindAsync(vm);
                error = IconError(vm.IconFile != null, vm.PresetIcon, required: false)
                        ?? VideoError(vm.VideoFile != null, current.VideoId != null, vm.RemoveVideo, vm.VideoTitle, vm.VideoDescription)
                        ?? error;
                if (error != null) return error;

                vm.Id = id.Value;
                vm.VideoId = current.VideoId;
                vm.ExistingIcon = current.ExistingIcon;
                vm.ExistingVideo = current.ExistingVideo;
                await service.UpdateAsync(vm);
            }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<ICategoryService>(sp).DeleteAsync(id);

        // Areas/Admin/Controllers/CategoryController.ValidateIcon / ValidateVideo ilə eyni qaydalar
        private static string? IconError(bool hasFile, string? preset, bool required)
        {
            if (!string.IsNullOrEmpty(preset) && !CategoryIconPresets.IsValid(preset)) return "Seçilən ikon tapılmadı";
            if (required && !hasFile && string.IsNullOrEmpty(preset)) return "Hazır ikonlardan birini seçin və ya öz ikonunuzu yükləyin";
            return null;
        }

        private static string? VideoError(bool newFile, bool hasExisting, bool removing, string? title, string? description)
        {
            var willHaveVideo = !removing && (newFile || hasExisting);
            if (willHaveVideo)
            {
                if (string.IsNullOrWhiteSpace(title)) return "Videonun başlığını daxil edin";
                if (string.IsNullOrWhiteSpace(description)) return "Videonun təsvirini daxil edin";
            }
            else if (!removing && (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(description)))
                return "Başlıq yazmısınız — video faylını da seçin";
            return null;
        }
    }

    /// <summary>Kateqoriya videoları (SubService). Qayda: hər kateqoriyada bir video.</summary>
    public sealed class VideoSection : AdminSection
    {
        public override string Key => "videos";
        public override string Title => "Xidmət videoları";
        public override string? Subtitle => "Hər kateqoriyada bir video";
        public override string Group => "Xidmətlər";

        private static AdminFieldDto[] Fields(IReadOnlyList<AdminOptionDto> categories) => new[]
        {
            new AdminFieldDto("CategoryId", "Kateqoriya", "select", true, "Yalnız videosu olmayan kateqoriyalar", Options: categories),
            Text("Title", "Başlıq", required: true),
            Multi("Description", "Təsvir", required: true),
            Video("VideoFile", "Video", required: true),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp)
        {
            var db = Get<AppDbContext>(sp);
            var blocked = await db.ServiceCategories.AnyAsync(c => !c.SubServices.Any())
                ? null
                : "Bütün kateqoriyaların videosu var. Yeni video üçün əvvəlcə mövcud videolardan birini silin.";
            return new((await Get<ISubServiceService>(sp).GetAllAsync())
                .Select(s => new AdminItemDto(s.Id, s.Title, s.CategoryName, null, s.IsExtra ? "Artıq" : null)).ToList(), blocked);
        }

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null)
                return Form(null, Fields(await CategoriesAsync(sp, null)),
                    new[] { ("CategoryId", (string?)""), ("Title", ""), ("Description", "") });

            var s = await Get<ISubServiceService>(sp).GetForEditAsync(id.Value);
            return s == null ? null : Form(s.Id, Fields(await CategoriesAsync(sp, s.CategoryId)),
                new[] { ("CategoryId", (string?)s.CategoryId.ToString()), ("Title", s.Title), ("Description", s.Description) },
                new[] { ("VideoFile", U("subvideos", s.ExistingVideo)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var service = Get<ISubServiceService>(sp);
            if (id == null)
            {
                var vm = new SubServiceCreateVM();
                var error = await b.BindAsync(vm);
                if (vm.VideoFile == null) return "Video faylını seçin";
                error = await CategoryErrorAsync(sp, vm.CategoryId, null) ?? error;
                if (error != null) return error;
                await service.CreateAsync(vm);
            }
            else
            {
                var current = await service.GetForEditAsync(id.Value);
                if (current == null) return "Video tapılmadı.";
                var vm = new SubServiceEditVM();
                var error = await b.BindAsync(vm);
                if (vm.CategoryId != current.CategoryId)
                    error = await CategoryErrorAsync(sp, vm.CategoryId, id.Value) ?? error;
                if (error != null) return error;
                vm.Id = id.Value;
                vm.ExistingVideo = current.ExistingVideo;
                await service.UpdateAsync(vm);
            }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<ISubServiceService>(sp).DeleteAsync(id);

        /// <summary>Videosu olmayan kateqoriyalar (+ redaktə olunan videonun öz kateqoriyası).</summary>
        private static async Task<IReadOnlyList<AdminOptionDto>> CategoriesAsync(IServiceProvider sp, int? current) =>
            await Get<AppDbContext>(sp).ServiceCategories.AsNoTracking()
                .Where(c => !c.SubServices.Any() || c.Id == current)
                .OrderBy(c => c.Id)
                .Select(c => new AdminOptionDto(c.Id.ToString(), c.Name))
                .ToListAsync();

        private static async Task<string?> CategoryErrorAsync(IServiceProvider sp, int categoryId, int? exceptId)
        {
            if (categoryId <= 0) return "Kateqoriya seçin";
            var busy = await Get<AppDbContext>(sp).SubServices.AnyAsync(s => s.CategoryId == categoryId && s.Id != exceptId);
            return busy ? "Bu kateqoriyanın artıq videosu var. Hər kateqoriyada bir video ola bilər." : null;
        }
    }

    /// <summary>Rezervasiya formasındakı xidmətlər.</summary>
    public sealed class ServiceSection : AdminSection
    {
        public override string Key => "services";
        public override string Title => "Rezervasiya xidmətləri";
        public override string? Subtitle => "Rezervasiya formasındakı xidmət siyahısı";
        public override string Group => "Xidmətlər";

        private static readonly AdminFieldDto[] Fields = { Text("Name", "Xidmətin adı", required: true, max: 100) };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            new((await Get<IServiceService>(sp).GetAllAsync())
                .Select(s => new AdminItemDto(s.Id, s.Name, null, null, null)).ToList(), null);

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, new[] { ("Name", (string?)"") });
            var s = await Get<IServiceService>(sp).GetByIdAsync(id.Value);
            return s == null ? null : Form(s.Id, Fields, new[] { ("Name", (string?)s.Name) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var vm = new ServiceVM();
            var error = await b.BindAsync(vm);
            if (error != null) return error;
            var service = Get<IServiceService>(sp);
            if (id == null) await service.CreateAsync(vm);
            else { vm.Id = id.Value; await service.UpdateAsync(vm); }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<IServiceService>(sp).DeleteAsync(id);
    }

    public sealed class TechnicianSection : AdminSection
    {
        public override string Key => "technicians";
        public override string Title => "Ustalar";
        public override string? Subtitle => "“Komandamız” bölməsi";
        public override string Group => "Komanda";

        private static readonly AdminFieldDto[] Fields =
        {
            Image("Photo", "Şəkil", required: true),
            Text("FullName", "Ad Soyad", required: true),
            Text("Position", "Vəzifə", required: true),
            Multi("Bio", "Təqdimat", max: 300),
            Url("FacebookUrl", "Facebook"),
            Url("InstagramUrl", "Instagram"),
            Url("TiktokUrl", "TikTok"),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            new((await Get<ITechnicianService>(sp).GetAllAsync())
                .Select(t => new AdminItemDto(t.Id, t.FullName ?? "", t.Position, U("technicians", t.Image), null)).ToList(), null);

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, Fields.Where(f => f.Type != "image").Select(f => (f.Name, (string?)"")));
            var t = await Get<ITechnicianService>(sp).GetByIdAsync(id.Value);
            return t == null ? null : Form(t.Id, Fields,
                new[]
                {
                    ("FullName", (string?)t.FullName), ("Position", t.Position), ("Bio", t.Bio),
                    ("FacebookUrl", t.FacebookUrl), ("InstagramUrl", t.InstagramUrl), ("TiktokUrl", t.TiktokUrl)
                },
                new[] { ("Photo", U("technicians", t.Image)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var service = Get<ITechnicianService>(sp);
            if (id == null)
            {
                var vm = new TechnicianCreateVM();
                var error = await b.BindAsync(vm);
                if (vm.Photo == null) return "Şəkil seçin";
                if (error != null) return error;
                await service.CreateAsync(vm);
            }
            else
            {
                var current = await service.GetByIdAsync(id.Value);
                if (current == null) return "Usta tapılmadı.";
                var vm = new TechnicianEditVM();
                var error = await b.BindAsync(vm);
                if (error != null) return error;
                vm.Id = id.Value;
                vm.ExistingImage = current.Image;
                await service.EditAsync(vm);
            }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<ITechnicianService>(sp).DeleteAsync(id);
    }

    /// <summary>Zəmanət sənədləri: yaradılır və silinir, redaktə olunmur (saytdakı kimi).</summary>
    public sealed class WarrantySection : AdminSection
    {
        public override string Key => "warranties";
        public override string Title => "Zəmanətlər";
        public override string? Subtitle => "Müştərilərə verilən zəmanət sənədləri";
        public override string Group => "Sənədlər";
        public override bool CanEdit => false;

        private static readonly AdminFieldDto[] Fields =
        {
            Text("CustomerFullName", "Müştərinin adı", required: true, max: 100),
            Text("VehicleModel", "Avtomobil modeli", required: true, max: 100),
            new("VehicleYear", "Buraxılış ili", "number", true),
            Multi("Terms", "Zəmanət şərtləri", required: true),
            new("Items", "Detallar və zəmanət müddəti (ay)", "items", true, "Ən azı bir detal; müddət 1–120 ay"),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            new((await Get<IWarrantyService>(sp).GetAllAsync())
                .OrderByDescending(w => w.Id)
                .Select(w => new AdminItemDto(w.Id, w.CustomerFullName ?? "", $"{w.VehicleModel} · {w.VehicleYear}", null,
                    w.CreatedDate.ToString("dd.MM.yyyy"))).ToList(), null);

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null)
                return Form(null, Fields, new[]
                {
                    ("CustomerFullName", (string?)""), ("VehicleModel", ""), ("VehicleYear", AzTime.Now.Year.ToString()), ("Terms", ""),
                    ("Items", JsonSerializer.Serialize(new[] { new AdminWarrantyItemDto("", 6) }, JsonOptions))
                });

            var w = await Get<IWarrantyService>(sp).GetByIdAsync(id.Value);
            if (w == null) return null;
            var items = (w.Items ?? new()).Select(i => new AdminWarrantyItemDto(i.PartName, i.WarrantyMonth)).ToList();
            return Form(w.Id, Fields, new[]
            {
                ("CustomerFullName", (string?)w.CustomerFullName), ("VehicleModel", w.VehicleModel),
                ("VehicleYear", w.VehicleYear.ToString()), ("Terms", w.Terms),
                ("Items", JsonSerializer.Serialize(items, JsonOptions))
            }, readOnly: true);
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            if (id != null) return "Zəmanət sənədi redaktə olunmur.";
            var vm = new WarrantyCreateVM();
            var error = await b.BindAsync(vm);
            if (vm.Items.Count == 0) return "Ən azı bir detal əlavə edin";
            if (error != null) return error;
            await Get<IWarrantyService>(sp).CreateAsync(vm);
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<IWarrantyService>(sp).DeleteAsync(id);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    }
}
