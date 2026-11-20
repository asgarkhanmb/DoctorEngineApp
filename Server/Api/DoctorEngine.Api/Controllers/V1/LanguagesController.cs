using DoctorEngine.Contracts;
using DoctorEngineApp.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Dillər (saytdakı Language/Set və Language/Js qarşılığı).
    /// API dili cookie ilə deyil, hər sorğuda Accept-Language başlığı ilə seçir (az/en/ru/tr).
    /// </summary>
    [ApiController]
    [Route("api/v1/languages")]
    [Produces("application/json")]
    [AllowAnonymous]
    public class LanguagesController : ControllerBase
    {
        /// <summary>Dəstəklənən dillər.</summary>
        [HttpGet]
        public ActionResult<IReadOnlyList<LanguageDto>> List() =>
            Ok(L.Languages.Select(l => new LanguageDto(l.Code, l.Name)).ToList());

        /// <summary>
        /// Saytın JS mətnləri (window.DE_I18N) seçilmiş dildə: { "azərbaycanca mətn": "tərcümə" }.
        /// az üçün boşdur — mətn özü azərbaycancadır.
        /// </summary>
        [HttpGet("{lang}/strings")]   // (ui — bütün mətnlər üçün aşağıya bax)
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public ActionResult<IReadOnlyDictionary<string, string>> Strings(string lang)
        {
            if (!L.IsSupported(lang)) return NotFound(new ApiError(L.T("Dil dəstəklənmir."), 404));
            return Ok(L.JsStrings(lang));
        }

        /// <summary>
        /// JavaScript front üçün BÜTÜN UI mətnləri: açarlı ("nav.home") və azərbaycanca mətn → tərcümə.
        /// Bazadakı məzmun (slayder, xidmət ...) content/* endpoint-lərində artıq tərcümə olunmuş gəlir.
        /// </summary>
        [HttpGet("{lang}/ui")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public ActionResult<Dictionary<string, string>> Ui(string lang)
        {
            if (!L.IsSupported(lang)) return NotFound(new ApiError(L.T("Dil dəstəklənmir."), 404));
            return Ok(L.AllStrings(lang));
        }
    }
}
