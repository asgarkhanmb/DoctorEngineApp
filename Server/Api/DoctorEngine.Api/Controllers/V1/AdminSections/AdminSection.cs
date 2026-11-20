using DoctorEngine.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngine.Api.Controllers.V1.AdminSections
{
    /// <summary>
    /// Mobil admin paneldə bir məzmun bölməsi. Hər bölmə saytın admin panelindəki eyni servisi və VM-i istifadə edir.
    /// </summary>
    public abstract class AdminSection
    {
        public abstract string Key { get; }
        public abstract string Title { get; }
        public virtual string? Subtitle => null;
        public abstract string Group { get; }

        /// <summary>"list" — siyahı; "single" — tək qeyd (ayarlar).</summary>
        public virtual string Kind => "list";
        public virtual bool CanCreate => Kind == "list";
        public virtual bool CanEdit => true;
        public virtual bool CanDelete => Kind == "list";

        public AdminSectionDto ToDto() => new(Key, Title, Subtitle, Group, Kind, CanCreate, CanEdit, CanDelete);

        public virtual Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            Task.FromResult(new AdminListDto(Array.Empty<AdminItemDto>(), null));

        /// <summary>id null — yeni qeyd (siyahı) və ya hazırkı qeyd (tək qeyd).</summary>
        public abstract Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id);

        /// <summary>Yaradır / yeniləyir. Xəta varsa — istifadəçiyə göstəriləcək mətn, yoxsa null.</summary>
        public abstract Task<string?> SaveAsync(IServiceProvider sp, AdminBinder binder, int? id);

        public virtual Task DeleteAsync(IServiceProvider sp, int id) => Task.CompletedTask;

        // ---------- köməkçilər ----------

        protected static T Get<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

        /// <summary>Faylın sayt kökünə nisbi ünvanı (bax ContentController.Upload).</summary>
        protected static string? U(string folder, string? value) => ContentController.Upload(folder, value);

        protected static AdminFieldDto Text(string name, string label, bool required = false, int? max = null, string? hint = null) =>
            new(name, label, "text", required, hint, max);
        protected static AdminFieldDto Multi(string name, string label, bool required = false, int? max = null, string? hint = null) =>
            new(name, label, "multiline", required, hint, max);
        protected static AdminFieldDto Url(string name, string label, bool required = false, string? hint = null) =>
            new(name, label, "url", required, hint ?? "https:// ilə başlamalıdır");
        protected static AdminFieldDto Phone(string name, string label, bool required = false) => new(name, label, "phone", required);
        protected static AdminFieldDto Email(string name, string label, bool required = false) => new(name, label, "email", required);
        /// <summary>Şəkil / video: Required — hazırkı fayl yoxdursa məcburidir.</summary>
        protected static AdminFieldDto Image(string name, string label, bool required = false, string? hint = null) =>
            new(name, label, "image", required, hint);
        protected static AdminFieldDto Video(string name, string label, bool required = false, string? hint = null) =>
            new(name, label, "video", required, hint);
        protected static AdminFieldDto Bool(string name, string label, string mode = "all") =>
            new(name, label, "bool", Mode: mode);

        protected static AdminFormDto Form(int? id, IReadOnlyList<AdminFieldDto> fields,
            IEnumerable<(string, string?)> values, IEnumerable<(string, string?)>? files = null, bool readOnly = false) =>
            new(id, fields,
                values.ToDictionary(v => v.Item1, v => v.Item2),
                (files ?? Array.Empty<(string, string?)>()).ToDictionary(v => v.Item1, v => v.Item2),
                readOnly);

        protected static string Short(string? text, int max = 90)
        {
            var t = (text ?? "").ReplaceLineEndings(" ").Trim();
            return t.Length <= max ? t : t[..max] + "…";
        }
    }

    /// <summary>Form məlumatını (mətn + fayllar) saytın VM-inə bağlayır və DataAnnotations qaydalarını yoxlayır.</summary>
    public sealed class AdminBinder
    {
        private readonly ControllerBase _controller;

        public AdminBinder(ControllerBase controller) => _controller = controller;

        /// <summary>Uğurludursa null, yoxsa ilk xəta mətni.</summary>
        public async Task<string?> BindAsync<T>(T model) where T : class
        {
            _controller.ModelState.Clear();
            if (await _controller.TryUpdateModelAsync(model)) return null;
            return _controller.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Məlumatları düzgün doldurun." : e.ErrorMessage)
                .FirstOrDefault() ?? "Məlumatları düzgün doldurun.";
        }
    }

    public static class AdminSectionRegistry
    {
        /// <summary>Tətbiqdəki menyu sırası.</summary>
        public static readonly IReadOnlyList<AdminSection> All = new AdminSection[]
        {
            new SliderSection(),
            new InformSection(),
            new AboutSection(),
            new CategorySection(),
            new VideoSection(),
            new ServiceSection(),
            new TechnicianSection(),
            new ContactSection(),
            new PhoneSection(),
            new MapSection(),
            new WhatsAppSection(),
            new WarrantySection(),
            new SiteSection(),
        };

        public static AdminSection? Find(string key) =>
            All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
