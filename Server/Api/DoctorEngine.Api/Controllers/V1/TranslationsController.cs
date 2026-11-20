using DoctorEngine.Contracts;
using DoctorEngineApp.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Bazadakı məzmunun AI tərcümələri (az → en/ru/tr). Sayt (front) bazaya qoşulmur:
    /// tərcümələri buradan yükləyir, tərcüməsi olmayan mətnləri isə növbəyə göndərir.
    /// </summary>
    [ApiController]
    [Route("api/v1/translations")]
    [Produces("application/json")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class TranslationsController : ControllerBase
    {
        private readonly TranslationStore _store;
        private readonly IConfiguration _config;

        public TranslationsController(TranslationStore store, IConfiguration config)
        {
            _store = store;
            _config = config;
        }

        /// <summary>Dilin bütün tərcümələri: { "azərbaycanca mətn": "tərcümə" }.</summary>
        [HttpGet("{lang}")]
        public ActionResult<Dictionary<string, string>> Get(string lang)
        {
            if (!TranslationStore.TargetLangs.Contains(lang))
                return NotFound(new ApiError(L.T("Dil dəstəklənmir."), 404));
            return Ok(_store.Snapshot(lang));
        }

        public record TranslationRequest(List<string> Texts);

        /// <summary>
        /// Tərcüməsi olmayan mətnləri AI növbəsinə əlavə edir.
        /// "Translations:RequestKey" ayarlanıbsa, "X-Translation-Key" başlığı tələb olunur (yalnız sayt göndərsin).
        /// </summary>
        [HttpPost("request")]
        [EnableRateLimiting("auth")]
        public IActionResult RequestTranslations([FromBody] TranslationRequest request)
        {
            var key = _config["Translations:RequestKey"];
            if (!string.IsNullOrEmpty(key) && Request.Headers["X-Translation-Key"] != key)
                return StatusCode(403, new ApiError(L.T("Bu əməliyyat üçün icazəniz yoxdur."), 403));

            foreach (var text in (request.Texts ?? new()).Take(200))
                if (TranslationStore.NeedsTranslation(text))
                    _store.Request(text);
            return NoContent();
        }
    }
}
