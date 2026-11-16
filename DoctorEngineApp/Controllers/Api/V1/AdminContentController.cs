using DoctorEngine.Contracts;
using DoctorEngineApp.Controllers.Api.V1.AdminSections;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers.Api.V1
{
    /// <summary>
    /// Mobil admin panelin məzmun bölmələri (slayder, xidmət, usta, əlaqə, ayarlar və s.).
    /// Hər bölmənin sahələri (sxem) serverdən gəlir — tətbiq formanı ondan qurur.
    /// Saxlama: multipart/form-data, sahə adları saytın admin formalarındakı ilə eynidir (eyni VM, eyni yoxlama).
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/sections")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [IgnoreAntiforgeryToken]   // yalnız Bearer — CSRF riski yoxdur
    public class AdminContentController : ControllerBase
    {
        private readonly ILogger<AdminContentController> _logger;

        public AdminContentController(ILogger<AdminContentController> logger) => _logger = logger;

        [HttpGet]
        public ActionResult<IReadOnlyList<AdminSectionDto>> Sections() =>
            Ok(AdminSectionRegistry.All.Select(s => s.ToDto()).ToList());

        [HttpGet("{key}")]
        public Task<IActionResult> List(string key) =>
            Run(key, async s => Ok(await s.ListAsync(HttpContext.RequestServices)));

        /// <summary>Forma: id yoxdursa — yeni qeyd (siyahı bölməsi) və ya tək qeyd (ayarlar bölməsi).</summary>
        [HttpGet("{key}/form")]
        public Task<IActionResult> Form(string key, [FromQuery] int? id = null) =>
            Run(key, async s =>
            {
                var form = await s.FormAsync(HttpContext.RequestServices, id);
                return form == null ? Error(404, "Qeyd tapılmadı.") : Ok(form);
            });

        /// <summary>Yeni qeyd (siyahı) və ya ayarları yadda saxla (tək qeyd).</summary>
        [HttpPost("{key}")]
        public Task<IActionResult> Create(string key) =>
            Run(key, async s =>
            {
                if (s.Kind == "list" && !s.CanCreate) return Error(405, "Bu bölmədə yeni qeyd əlavə olunmur.");
                var error = await s.SaveAsync(HttpContext.RequestServices, new AdminBinder(this), null);
                return error == null ? NoContent() : Error(400, error);
            });

        [HttpPost("{key}/{id:int}")]
        public Task<IActionResult> Update(string key, int id) =>
            Run(key, async s =>
            {
                if (!s.CanEdit) return Error(405, "Bu bölmədə redaktə mümkün deyil.");
                var error = await s.SaveAsync(HttpContext.RequestServices, new AdminBinder(this), id);
                return error == null ? NoContent() : Error(400, error);
            });

        [HttpDelete("{key}/{id:int}")]
        public Task<IActionResult> Delete(string key, int id) =>
            Run(key, async s =>
            {
                if (!s.CanDelete) return Error(405, "Bu bölmədə silmə mümkün deyil.");
                await s.DeleteAsync(HttpContext.RequestServices, id);
                return NoContent();
            });

        private async Task<IActionResult> Run(string key, Func<AdminSection, Task<IActionResult>> action)
        {
            var section = AdminSectionRegistry.Find(key);
            if (section == null) return Error(404, "Bölmə tapılmadı.");
            try
            {
                return await action(section);
            }
            catch (Exception ex) when (ex.GetType() == typeof(Exception))
            {
                // Servislər istifadəçiyə göstəriləcək xətanı adi Exception ilə atır (məs. "Əlaqə məlumatı tapılmadı")
                _logger.LogWarning(ex, "Admin bölməsi {Key}: {Message}", key, ex.Message);
                return Error(400, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin bölməsi {Key} xəta verdi", key);
                return Error(500, "Əməliyyat alınmadı. Yenidən cəhd edin.");
            }
        }

        private ObjectResult Error(int code, string message) => StatusCode(code, new ApiError(message, code));
    }
}
