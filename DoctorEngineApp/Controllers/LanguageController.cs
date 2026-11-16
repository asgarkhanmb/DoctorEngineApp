using DoctorEngineApp.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    /// <summary>Sayt dilini dəyişir: /Language/Set?lang=en&amp;returnUrl=/Service</summary>
    public class LanguageController : Controller
    {
        [HttpGet]
        public IActionResult Set(string lang, string? returnUrl)
        {
            if (L.IsSupported(lang))
            {
                Response.Cookies.Append(L.CookieName, lang, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    HttpOnly = false,   // gizli deyil; oflayn səhifə (offline.html) dili buradan oxuyur
                    Secure = Request.IsHttps
                });
            }

            // Yalnız saytın öz səhifələrinə qayıt (open redirect qorunması)
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// JS mətnləri: window.DE_I18N + deT("azərbaycanca mətn", arg0, ...) funksiyası.
        /// URL-də versiya (v) olduğu üçün brauzer bir il keşləyir.
        /// </summary>
        [HttpGet]
        // VaryByQueryKeys yoxdur: o, ResponseCaching middleware tələb edir (onsuz 500 qaytarır). URL-dəki lang+v onsuz da unikaldır.
        [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
        public IActionResult Js(string lang)
        {
            if (!L.IsSupported(lang)) lang = L.Default;
            var json = System.Text.Json.JsonSerializer.Serialize(L.JsStrings(lang));
            var js =
                $"window.DE_LANG=\"{lang}\";window.DE_I18N={json};" +
                "window.deT=function(s){var a=arguments,t=(window.DE_I18N&&window.DE_I18N[s])||s;" +
                "return t.replace(/\\{(\\d+)\\}/g,function(m,i){var v=a[+i+1];return v===undefined?m:v;});};";
            return Content(js, "text/javascript; charset=utf-8");
        }
    }
}
