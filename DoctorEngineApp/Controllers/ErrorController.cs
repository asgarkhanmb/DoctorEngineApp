using System.Diagnostics;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    /// <summary>
    /// Bütün xəta səhifələri:
    ///   /Error        — tutulmamış exception (Program.cs → UseExceptionHandler)
    ///   /Error/{code} — 404, 403, 500 və s. (Program.cs → UseStatusCodePagesWithReExecute)
    /// Səhifə verilənlər bazasından asılı deyil (Layout yoxdur) — baza işləməyəndə də açılır.
    /// API sorğularına HTML yox, JSON qaytarılır.
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]                 // POST sorğusu xəta səhifəsinə yönələndə token yoxlanmasın
    [ApiExplorerSettings(IgnoreApi = true)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;
        private readonly IServiceProvider _services;

        public ErrorController(ILogger<ErrorController> logger, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
        }

        [Route("Error")]
        public async Task<IActionResult> Exception()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var path = feature?.Path ?? HttpContext.Request.Path;

            if (feature?.Error != null)
                _logger.LogError(feature.Error, "Tutulmamış xəta: {Method} {Path} (sorğu {RequestId})",
                    HttpContext.Request.Method, path, RequestId);

            // Sorğu ölçüsü limiti aşılıbsa (BadHttpRequestException 413) — uyğun səhifə
            var code = feature?.Error is BadHttpRequestException bad ? bad.StatusCode : 500;
            return await Render(code, path);
        }

        [Route("Error/{code:int}")]
        public async Task<IActionResult> Status(int code)
        {
            if (code < 400 || code > 599) code = 404;
            var reExecute = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var path = reExecute?.OriginalPath ?? HttpContext.Request.Path.Value ?? "/";
            return await Render(code, path);
        }

        private async Task<IActionResult> Render(int code, string? originalPath)
        {
            var vm = ErrorPages.For(code);
            vm.RequestId = RequestId;
            vm.SiteName = await SafeSiteNameAsync();
            if (vm.ShowLogin && !(User.Identity?.IsAuthenticated ?? false))
                vm.LoginUrl = Url.Action("Login", "Account", new { returnUrl = originalPath });

            Response.StatusCode = code;

            // API və ya AJAX: JSON
            var wantsJson = (originalPath ?? "").StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
                            Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            (Request.Headers.Accept.ToString().Contains("application/json") &&
                             !Request.Headers.Accept.ToString().Contains("text/html"));
            if (wantsJson)
                return new JsonResult(new { error = vm.Message, code, requestId = vm.RequestId }) { StatusCode = code };

            return View("Status", vm);
        }

        private string RequestId => Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        // Sayt adı bazadan gəlir; baza işləmirsə standart ad
        private async Task<string> SafeSiteNameAsync()
        {
            try
            {
                var settings = _services.GetService<ISiteSettingService>();
                if (settings != null)
                {
                    var site = await settings.GetAsync();
                    if (!string.IsNullOrWhiteSpace(site?.SiteName)) return site.SiteName;
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Xəta səhifəsi: sayt adı oxunmadı");
            }
            return "DoctorEngine.az";
        }
    }
}
