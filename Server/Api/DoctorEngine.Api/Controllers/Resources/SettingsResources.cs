using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Admin;
using DoctorEngineApp.ViewModels.ContactInfos;
using DoctorEngineApp.ViewModels.ContactMessage;
using DoctorEngineApp.ViewModels.ContactPhones;
using DoctorEngineApp.ViewModels.Maps;
using DoctorEngineApp.ViewModels.Reservations;
using DoctorEngineApp.ViewModels.Warranties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.Resources
{
    // =====================================================================
    // Əlaqə, xəritə, WhatsApp, sayt ayarları, əlaqə mesajları, zəmanətlər,
    // rezervasiyalar (sayt formaları üçün) və admin paneli statistikası.
    // =====================================================================

    [Route("api/v1/contact-info")]
    public class ContactInfoController : ResourceController
    {
        private readonly IContactInfoService _service;
        public ContactInfoController(IContactInfoService service) => _service = service;

        /// <summary>Saytda göstərilən əlaqə məlumatı (yoxdursa boş obyekt).</summary>
        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<ContactInfoVm>> Get() => await _service.GetFirstAsync() ?? new ContactInfoVm();

        [HttpGet("exists"), AllowAnonymous]
        public async Task<ActionResult<bool>> Exists() => await _service.ExistsAsync();

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] ContactInfoVm vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromForm] ContactInfoVm vm)
        {
            await _service.UpdateAsync(vm);
            return NoContent();
        }
    }

    [Route("api/v1/contact-phones")]
    public class ContactPhonesController : ResourceController
    {
        private readonly IContactPhoneService _service;
        public ContactPhonesController(IContactPhoneService service) => _service = service;

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<List<ContactPhoneVM>>> List() => await _service.GetAllAsync();

        [HttpGet("{id:int}"), AllowAnonymous]
        public async Task<ActionResult<ContactPhoneVM>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] ContactPhoneVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] ContactPhoneVM vm)
        {
            vm.Id = id;
            await _service.UpdateAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/map")]
    public class MapResourceController : ResourceController
    {
        private readonly IMapService _service;
        public MapResourceController(IMapService service) => _service = service;

        /// <summary>Xəritə (yoxdursa 204).</summary>
        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<MapVM?>> Get() => await _service.GetSingleAsync();

        [HttpGet("exists"), AllowAnonymous]
        public async Task<ActionResult<bool>> Exists() => await _service.ExistsAsync();

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] MapVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromForm] MapVM vm)
        {
            await _service.UpdateAsync(vm);
            return NoContent();
        }
    }

    [Route("api/v1/whatsapp")]
    public class WhatsappResourceController : ResourceController
    {
        private readonly IWhatsappService _service;
        public WhatsappResourceController(IWhatsappService service) => _service = service;

        /// <summary>WhatsApp ayarı (yoxdursa 204).</summary>
        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<WhatsappSetting?>> Get() => await _service.GetAsync();

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] WhatsappSetting model)
        {
            await _service.CreateAsync(model);
            return NoContent();
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromForm] WhatsappSetting model)
        {
            await _service.UpdateAsync(model);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var current = await _service.GetAsync();
            if (current == null || current.Id != id) return Error(404, "Qeyd tapılmadı.");
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/site-settings")]
    public class SiteSettingsController : ResourceController
    {
        private readonly ISiteSettingService _service;
        public SiteSettingsController(ISiteSettingService service) => _service = service;

        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<SiteSetting>> Get() => await _service.GetAsync();

        [HttpPut]
        public async Task<IActionResult> Save([FromForm] SiteSettingVM vm)
        {
            if (!SiteDefaults.IsAllowedIcon(vm.LogoIcon)) return Error(400, "Siyahıdan ikon seçin");
            await _service.SaveAsync(vm);
            return NoContent();
        }
    }

    /// <summary>Əlaqə formasından gələn mesajlar.</summary>
    [Route("api/v1/contact-messages")]
    public class ContactMessagesController : ResourceController
    {
        private readonly IContactMessageService _service;
        public ContactMessagesController(IContactMessageService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<ContactMessage>>> List() => await _service.GetAllAsync();

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ContactMessage>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        /// <summary>Saytın əlaqə forması (eyni email-dən 1 saatda 1 mesaj — servis yoxlayır).</summary>
        [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
        public async Task<IActionResult> Create([FromForm] ContactMessageCreateVM vm)
        {
            await _service.CreateAsync(vm);
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }

    [Route("api/v1/warranties")]
    public class WarrantiesController : ResourceController
    {
        private readonly IWarrantyService _service;
        public WarrantiesController(IWarrantyService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<Warranty>>> List() => await _service.GetAllAsync();

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Warranty>> Get(int id) => OkOrNotFound(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<ActionResult<Warranty>> Create([FromForm] WarrantyCreateVM vm) => await _service.CreateAsync(vm);

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>Zəmanət sənədi PDF (A4) — Views/Warranty/Print.cshtml.</summary>
        [HttpGet("{id:int}/pdf")]
        [Produces("application/pdf")]
        public async Task<IActionResult> Pdf(int id)
        {
            var warranty = await _service.GetByIdAsync(id);
            if (warranty == null) return Error(404, "Qeyd tapılmadı.");
            return new Rotativa.AspNetCore.ViewAsPdf("~/Views/Warranty/Print.cshtml", warranty)
            {
                FileName = $"zemanet_{id}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(10, 10, 10, 10)
            };
        }
    }

    /// <summary>Saytın rezervasiya forması (VM ilə) və admin siyahısı.</summary>
    [Route("api/v1/reservations")]
    public class ReservationResourcesController : ResourceController
    {
        private readonly IReservationService _service;
        public ReservationResourcesController(IReservationService service) => _service = service;

        /// <summary>Saytın formasından (form sahələri ReservationCreateVM ilə eyni). Cavab: { success, message, trackingCode }.</summary>
        [HttpPost("form"), AllowAnonymous, EnableRateLimiting("auth")]
        public async Task<ActionResult<ReservationResult>> CreateFromForm([FromForm] ReservationCreateVM vm) =>
            await _service.TryCreateAsync(vm);

        /// <summary>Kodlar üzrə statuslar (tam VM): GET ...?codes=A1B2&amp;codes=C3D4</summary>
        [HttpGet("lookup"), AllowAnonymous]
        public async Task<ActionResult<List<ReservationStatusVM>>> Lookup([FromQuery] string[] codes) =>
            await _service.LookupAsync((codes ?? Array.Empty<string>()).Take(50));

        [HttpGet("all")]
        public async Task<ActionResult<List<ReservationAdminVM>>> All() => await _service.GetAllAsync();
    }

    [Route("api/v1/admin/overview")]
    public class AdminOverviewController : ResourceController
    {
        private readonly AppDbContext _db;
        public AdminOverviewController(AppDbContext db) => _db = db;

        /// <summary>Admin panelin əsas səhifəsi (saytdakı DashboardVM — son mesajlar və çat daxil).</summary>
        [HttpGet]
        public async Task<ActionResult<DashboardVM>> Dashboard()
        {
            var today = AzTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var weekAgo = today.AddDays(-7);

            return new DashboardVM
            {
                PendingReservations = await _db.Reservations.CountAsync(r => r.Status == "Pending"),
                TodayReservations = await _db.Reservations.CountAsync(r => r.DateTime >= today && r.DateTime < tomorrow && r.Status != "Rejected"),
                TotalReservations = await _db.Reservations.CountAsync(),
                MessagesLast7Days = await _db.ContactMessages.CountAsync(m => m.CreatedDate >= weekAgo),
                TotalMessages = await _db.ContactMessages.CountAsync(),
                ChatMessagesToday = await _db.ChatMessages.CountAsync(m => m.CreatedDate >= today),
                Users = await _db.Users.CountAsync(),
                Warranties = await _db.Warranties.CountAsync(),
                UpcomingReservations = await _db.Reservations.AsNoTracking()
                    .Where(r => r.DateTime >= today && r.Status != "Rejected")
                    .OrderBy(r => r.DateTime)
                    .Take(6)
                    .Select(r => new DashboardReservationVM
                    {
                        Id = r.Id,
                        FullName = r.FullName,
                        Phone = r.Phone,
                        WhatsApp = r.WhatsApp,
                        ServiceName = r.Service.Name,
                        DateTime = r.DateTime,
                        Status = r.Status
                    })
                    .ToListAsync(),
                LatestMessages = await _db.ContactMessages.AsNoTracking()
                    .OrderByDescending(m => m.CreatedDate)
                    .Take(5)
                    .ToListAsync(),
                LatestChat = await _db.ChatMessages.AsNoTracking()
                    .Where(m => !m.SoftDeleted)
                    .OrderByDescending(m => m.Id)
                    .Take(5)
                    .ToListAsync()
            };
        }

        public record AdminBadgesDto(int PendingReservations, int NewMessages);

        /// <summary>Admin menyusundakı nişanlar: gözləyən rezervasiyalar, son 7 günün mesajları.</summary>
        [HttpGet("badges")]
        public async Task<ActionResult<AdminBadgesDto>> Badges()
        {
            var weekAgo = AzTime.Now.Date.AddDays(-7);
            return new AdminBadgesDto(
                await _db.Reservations.CountAsync(r => r.Status == "Pending"),
                await _db.ContactMessages.CountAsync(m => m.CreatedDate >= weekAgo));
        }
    }
}
