using DoctorEngine.Contracts;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Admin;
using DoctorEngineApp.ViewModels.ContactInfos;
using DoctorEngineApp.ViewModels.ContactPhones;
using DoctorEngineApp.ViewModels.Maps;

namespace DoctorEngine.Api.Controllers.V1.AdminSections
{
    // =====================================================================
    // Əlaqə və sayt ayarları. "single" — tək qeyd: yoxdursa yaradılır, varsa yenilənir.
    // =====================================================================

    public sealed class ContactSection : AdminSection
    {
        public override string Key => "contact";
        public override string Title => "Əlaqə məlumatları";
        public override string? Subtitle => "Ünvan, telefon, email, iş saatları, sosial şəbəkələr";
        public override string Group => "Əlaqə";
        public override string Kind => "single";

        private static readonly AdminFieldDto[] Fields =
        {
            Text("Address", "Ünvan", required: true),
            Phone("Phone", "Telefon", required: true),
            Email("Email", "Email", required: true),
            Text("Weekdays", "İş saatları (B.e – Cümə)", required: true, hint: "Məs: 10:00 - 20:00"),
            Text("Weekend", "İş saatları (Şənbə – Bazar)", required: true, hint: "Məs: 10:00 - 18:00"),
            Phone("WhatsApp", "WhatsApp"),
            Url("YouTube", "YouTube"),
            Url("Facebook", "Facebook"),
            Url("TikTok", "TikTok"),
        };

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            var c = await Get<IContactInfoService>(sp).GetFirstAsync();
            return Form(c?.Id, Fields, new[]
            {
                ("Address", c?.Address), ("Phone", c?.Phone), ("Email", c?.Email),
                ("Weekdays", c?.Weekdays), ("Weekend", c?.Weekend), ("WhatsApp", c?.WhatsApp),
                ("YouTube", c?.YouTube), ("Facebook", c?.Facebook), ("TikTok", c?.TikTok)
            });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var vm = new ContactInfoVm();
            var error = await b.BindAsync(vm);
            if (error != null) return error;

            var service = Get<IContactInfoService>(sp);
            var current = await service.GetFirstAsync();
            if (current == null) await service.CreateAsync(vm);
            else { vm.Id = current.Id; await service.UpdateAsync(vm); }
            return null;
        }
    }

    public sealed class PhoneSection : AdminSection
    {
        public override string Key => "phones";
        public override string Title => "Əlaqə telefonları";
        public override string? Subtitle => "Rezervasiya və əlaqə səhifələrindəki telefon kartları";
        public override string Group => "Əlaqə";

        private static readonly AdminFieldDto[] Fields =
        {
            Text("Subtitle", "Kiçik başlıq", required: true),
            Text("Title", "Başlıq", required: true),
            Multi("Description", "Mətn", required: true),
            Phone("PhoneNumber", "Telefon nömrəsi", required: true),
        };

        public override async Task<AdminListDto> ListAsync(IServiceProvider sp) =>
            new((await Get<IContactPhoneService>(sp).GetAllAsync())
                .Select(p => new AdminItemDto(p.Id, p.Title, p.PhoneNumber, null, null)).ToList(), null);

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            if (id == null) return Form(null, Fields, Fields.Select(f => (f.Name, (string?)"")));
            var p = await Get<IContactPhoneService>(sp).GetByIdAsync(id.Value);
            return p == null ? null : Form(p.Id, Fields, new[]
            {
                ("Subtitle", (string?)p.Subtitle), ("Title", p.Title), ("Description", p.Description), ("PhoneNumber", p.PhoneNumber)
            });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var vm = new ContactPhoneVM();
            var error = await b.BindAsync(vm);
            if (error != null) return error;
            var service = Get<IContactPhoneService>(sp);
            if (id == null) await service.CreateAsync(vm);
            else
            {
                if (await service.GetByIdAsync(id.Value) == null) return "Telefon tapılmadı.";
                vm.Id = id.Value;
                await service.UpdateAsync(vm);
            }
            return null;
        }

        public override Task DeleteAsync(IServiceProvider sp, int id) => Get<IContactPhoneService>(sp).DeleteAsync(id);
    }

    public sealed class MapSection : AdminSection
    {
        public override string Key => "map";
        public override string Title => "Xəritə";
        public override string? Subtitle => "Əlaqə səhifəsindəki Google xəritə";
        public override string Group => "Əlaqə";
        public override string Kind => "single";

        private static readonly AdminFieldDto[] Fields =
        {
            Text("Address", "Ünvan", required: true),
            Url("MapUrl", "Xəritə linki", required: true,
                hint: "Google Maps → Paylaş → Xəritəni yerləşdir → https:// ilə başlayan link"),
        };

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            var m = await Get<IMapService>(sp).GetSingleAsync();
            return Form(m?.Id, Fields, new[] { ("Address", m?.Address), ("MapUrl", m?.MapUrl) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var vm = new MapVM();
            var error = await b.BindAsync(vm);
            if (error != null) return error;
            var service = Get<IMapService>(sp);
            if (await service.ExistsAsync()) await service.UpdateAsync(vm);
            else await service.CreateAsync(vm);
            return null;
        }
    }

    public sealed class WhatsAppSection : AdminSection
    {
        public override string Key => "whatsapp";
        public override string Title => "WhatsApp";
        public override string? Subtitle => "Saytdakı WhatsApp düyməsi";
        public override string Group => "Əlaqə";
        public override string Kind => "single";

        private static readonly AdminFieldDto[] Fields =
        {
            Phone("PhoneNumber", "Telefon nömrəsi", required: true),
            Multi("Message", "Hazır mesaj", hint: "Müştəri düyməyə basanda WhatsApp-da yazılmış olur"),
        };

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            var w = await Get<IWhatsappService>(sp).GetAsync();
            return Form(w?.Id, Fields, new[] { ("PhoneNumber", w?.PhoneNumber), ("Message", w?.Message) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var input = new WhatsappSetting();
            var error = await b.BindAsync(input);
            if (string.IsNullOrWhiteSpace(input.PhoneNumber)) return "Telefon nömrəsini daxil edin";
            if (error != null) return error;

            var service = Get<IWhatsappService>(sp);
            var current = await service.GetAsync();   // izlənən (tracked) qeyd
            if (current == null)
            {
                await service.CreateAsync(new WhatsappSetting { PhoneNumber = input.PhoneNumber.Trim(), Message = input.Message ?? "" });
            }
            else
            {
                current.PhoneNumber = input.PhoneNumber.Trim();
                current.Message = input.Message ?? "";
                await service.UpdateAsync(current);
            }
            return null;
        }
    }

    public sealed class SiteSection : AdminSection
    {
        public override string Key => "site";
        public override string Title => "Sayt ayarları";
        public override string? Subtitle => "Saytın adı və loqosu";
        public override string Group => "Sayt";
        public override string Kind => "single";

        private static readonly AdminFieldDto[] Fields =
        {
            Text("SiteName", "Saytın adı", required: true, max: 60),
            new("LogoIcon", "Loqo ikonu", "select", true, "Loqo şəkli yoxdursa ikon göstərilir",
                Options: SiteDefaults.Icons.Select(i => new AdminOptionDto(i.Css, i.Label)).ToArray()),
            Image("LogoFile", "Loqo şəkli (ixtiyari)"),
            Bool("RemoveLogo", "Loqo şəklini sil"),
        };

        public override async Task<AdminFormDto?> FormAsync(IServiceProvider sp, int? id)
        {
            var s = await Get<ISiteSettingService>(sp).GetAsync();
            return Form(s.Id, Fields,
                new[] { ("SiteName", (string?)s.SiteName), ("LogoIcon", s.LogoIcon) },
                new[] { ("LogoFile", U("site", s.LogoImage)) });
        }

        public override async Task<string?> SaveAsync(IServiceProvider sp, AdminBinder b, int? id)
        {
            var vm = new SiteSettingVM();
            var error = await b.BindAsync(vm);
            if (!SiteDefaults.IsAllowedIcon(vm.LogoIcon)) return "Siyahıdan ikon seçin";
            if (error != null) return error;
            await Get<ISiteSettingService>(sp).SaveAsync(vm);
            return null;
        }
    }
}
