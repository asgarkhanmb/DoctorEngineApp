using DoctorEngineApp.Localization;
using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Categories;
using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Controllers.Api.V1
{
    /// <summary>
    /// Saytın məzmunu tətbiq üçün. Saytın istifadə etdiyi eyni servislərdən oxunur —
    /// admin paneldə dəyişən hər şey tətbiqdə də dərhal görünür.
    /// </summary>
    [ApiController]
    [Route("api/v1/content")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class ContentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ISiteSettingService _site;
        private readonly ISliderService _sliders;
        private readonly IInformService _informs;
        private readonly IAboutService _about;
        private readonly ITechnicianService _technicians;
        private readonly IContactInfoService _contact;
        private readonly IContactPhoneService _phones;
        private readonly IMapService _map;
        private readonly IWhatsappService _whatsapp;
        private readonly IContactMessageService _messages;

        public ContentController(AppDbContext db, ISiteSettingService site, ISliderService sliders, IInformService informs,
                                 IAboutService about, ITechnicianService technicians, IContactInfoService contact,
                                 IContactPhoneService phones, IMapService map, IWhatsappService whatsapp,
                                 IContactMessageService messages)
        {
            _db = db;
            _site = site;
            _sliders = sliders;
            _informs = informs;
            _about = about;
            _technicians = technicians;
            _contact = contact;
            _phones = phones;
            _map = map;
            _whatsapp = whatsapp;
            _messages = messages;
        }

        /// <summary>Ana səhifə üçün hamısı bir sorğuda (tətbiq açılışında bir dəfə yüklənir).</summary>
        [HttpGet("home")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]   // mətnlər dilə görə fərqlənir
        public async Task<ActionResult<HomeDto>> Home()
        {
            var site = await _site.GetAsync();
            var sliders = await _sliders.GetAllAsync();
            var informs = await _informs.GetAllAsync();
            var about = await _about.GetSingleAsync();
            var technicians = await _technicians.GetAllAsync();
            var contact = await _contact.GetFirstAsync();
            var phones = await _phones.GetAllAsync();
            var map = await _map.GetSingleAsync();
            var whatsapp = await _whatsapp.GetAsync();

            return Ok(new HomeDto(
                new SiteDto(
                    string.IsNullOrWhiteSpace(site?.SiteName) ? "DoctorEngine.az" : site!.SiteName,
                    site?.LogoIcon,
                    Upload("site", site?.LogoImage)),
                sliders.Select(s => new SlideDto(s.Id, L.T(s.Title), L.T(s.Subtitle), Upload("sliders", s.Image1), Upload("sliders", s.Image2))).ToList(),
                informs.Select(i => new InformDto(L.T(i.Title), L.T(i.Description), Upload("inform", i.Icon))).ToList(),
                about == null ? null : new AboutDto(L.T(about.Title), L.T(about.Description), Upload("about", about.VideoPath)),
                await LoadCategoriesAsync(),
                technicians.Select(ToDto).ToList(),
                contact == null ? null : new ContactDto(L.T(contact.Address), contact.Phone, contact.Email, contact.YouTube,
                    contact.Facebook, contact.TikTok, contact.WhatsApp, contact.Weekdays, contact.Weekend),
                phones.Select(p => new ContactPhoneDto(L.T(p.Title), L.T(p.Subtitle), L.T(p.Description), p.PhoneNumber)).ToList(),
                map == null ? null : new MapDto(L.T(map.Address), map.MapUrl),
                whatsapp == null || string.IsNullOrWhiteSpace(whatsapp.PhoneNumber) ? null
                    : new WhatsAppDto(whatsapp.PhoneNumber, L.T(whatsapp.Message),
                        $"https://wa.me/{whatsapp.PhoneNumber}?text={Uri.EscapeDataString(L.T(whatsapp.Message))}")));
        }

        /// <summary>Xidmət kateqoriyaları və videoları (Xidmətlər ekranı).</summary>
        [HttpGet("services")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]   // mətnlər dilə görə fərqlənir
        public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Services() => Ok(await LoadCategoriesAsync());

        [HttpGet("technicians")]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]   // mətnlər dilə görə fərqlənir
        public async Task<ActionResult<IReadOnlyList<TechnicianDto>>> Technicians() =>
            Ok((await _technicians.GetAllAsync()).Select(ToDto).ToList());

        /// <summary>Əlaqə forması (saytdakı ilə eyni qaydalar, məs. eyni email-dən 1 saatda 1 mesaj).</summary>
        [HttpPost("contact")]
        [IgnoreAntiforgeryToken]   // yalnız JSON — CSRF riski yoxdur
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
        public async Task<ActionResult<MessageResponse>> Contact([FromBody] ContactMessageRequest request)
        {
            var vm = new DoctorEngineApp.ViewModels.ContactMessage.ContactMessageCreateVM
            {
                FullName = request.FullName?.Trim() ?? "",
                Email = request.Email?.Trim() ?? "",
                Subject = request.Subject?.Trim() ?? "",
                Message = request.Message?.Trim() ?? ""
            };

            // Saytdakı DataAnnotations qaydaları
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(vm, new(vm), results, validateAllProperties: true))
                return BadRequest(new ApiError(results[0].ErrorMessage ?? L.T("Məlumatları düzgün doldurun."), 400));

            try
            {
                await _messages.CreateAsync(vm);
            }
            catch (Exception ex) when (ex.GetType() == typeof(Exception))
            {
                return BadRequest(new ApiError(ex.Message, 400));   // məs. "son 1 saat ərzində artıq mesaj göndərilib"
            }
            return Ok(new MessageResponse(L.T("Mesajınız uğurla göndərildi. Tezliklə sizinlə əlaqə saxlanılacaq.")));
        }

        // ================= KÖMƏKÇİLƏR =================

        private async Task<IReadOnlyList<CategoryDto>> LoadCategoriesAsync()
        {
            // Ana səhifə ilə eyni sorğu (HomeController)
            var categories = await _db.ServiceCategories
                .AsNoTracking()
                .Include(c => c.SubServices)
                .OrderBy(c => c.Id)
                .Select(c => new CategoryVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    Icon = c.Icon,
                    SubServices = c.SubServices.OrderBy(s => s.Id).Select(s => new SubServiceVM
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Description = s.Description,
                        Video = s.Video
                    }).ToList()
                }).ToListAsync();

            var services = await _db.Services.AsNoTracking().Select(s => new ServiceVM { Id = s.Id, Name = s.Name }).ToListAsync();
            var reserveMap = ServiceGridVM.MapReservationIds(categories, services);

            return categories.Select(c => new CategoryDto(
                c.Id,
                L.T(c.Name),
                Upload("categories", c.Icon),
                (c.SubServices ?? new()).Select(s => new VideoDto(s.Id, L.T(s.Title), L.T(s.Description), Upload("subvideos", s.Video))).ToList(),
                reserveMap.TryGetValue(c.Id, out var sid) ? sid : null)).ToList();
        }

        private static TechnicianDto ToDto(Technician t) => new(
            t.Id, t.FullName ?? "", L.T(t.Position), L.T(t.Bio), Upload("technicians", t.Image),
            t.FacebookUrl, t.InstagramUrl, t.TiktokUrl);

        /// <summary>
        /// Fayl ünvanını sayt kökünə nisbi yola çevirir. Bəzi sahələrdə yalnız fayl adı ("x.jpg"),
        /// bəzilərində tam yol ("/uploads/..." və ya "~/uploads/...") saxlanılır.
        /// </summary>
        public static string? Upload(string folder, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return value;
            if (value.StartsWith("~/")) return value[1..];
            if (value.StartsWith('/')) return value;
            return $"/uploads/{folder}/{value}";
        }
    }
}
