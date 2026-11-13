using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Admin panelində hər hansı əməliyyat xəta versə (məs. silinmiş element, əlaqəli məlumat, fayl xətası),
    /// istifadəçi "xəta səhifəsi" görmür: xəta loga yazılır, səliqəli bildiriş (qırmızı toast) göstərilir
    /// və əvvəlki səhifəyə (yoxdursa — bölmənin siyahısına) qaytarılır.
    /// </summary>
    public class AdminExceptionFilter : IExceptionFilter
    {
        private readonly ITempDataDictionaryFactory _tempData;
        private readonly ILogger<AdminExceptionFilter> _logger;

        public AdminExceptionFilter(ITempDataDictionaryFactory tempData, ILogger<AdminExceptionFilter> logger)
        {
            _tempData = tempData;
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var http = context.HttpContext;
            if (!string.Equals(context.RouteData.Values["area"]?.ToString(), "Admin", StringComparison.OrdinalIgnoreCase))
                return;   // sayt tərəfi — qlobal xəta səhifəsi (ErrorController)
            if (context.Exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
                return;   // istifadəçi səhifəni bağlayıb
            // Panelin əsas səhifəsi özü açılmırsa (məs. baza işləmir) — yönləndirmə dövrə girməsin, xəta səhifəsi göstərilsin
            if (HttpMethods.IsGet(http.Request.Method) &&
                string.Equals(context.RouteData.Values["controller"]?.ToString(), "Dashboard", StringComparison.OrdinalIgnoreCase))
                return;

            _logger.LogError(context.Exception, "Admin əməliyyatı xəta verdi: {Method} {Path}",
                http.Request.Method, http.Request.Path);

            // id ilə açılan element yoxdursa (silinib) servislər çox vaxt null ilə işləyib NullReferenceException atır
            var missingItem = context.Exception is NullReferenceException && context.RouteData.Values.ContainsKey("id");
            _tempData.GetTempData(http)["Error"] = missingItem
                ? "Element tapılmadı — artıq silinmiş ola bilər. Siyahını yeniləyin."
                : FriendlyMessage(context.Exception);

            // Əvvəlki səhifəyə qayıt (yalnız eyni sayt daxilində), yoxdursa — bölmənin əsas səhifəsi
            var referer = http.Request.Headers.Referer.ToString();
            var sameSite = Uri.TryCreate(referer, UriKind.Absolute, out var refUri) &&
                           string.Equals(refUri.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(refUri.AbsolutePath, http.Request.Path, StringComparison.OrdinalIgnoreCase);

            var controller = context.RouteData.Values["controller"]?.ToString();
            var isIndex = string.Equals(context.RouteData.Values["action"]?.ToString(), "Index", StringComparison.OrdinalIgnoreCase);

            context.Result = sameSite
                ? new RedirectResult(refUri!.PathAndQuery)
                : isIndex
                    ? new RedirectToActionResult("Index", "Dashboard", new { area = "Admin" })   // siyahının özü açılmırsa — panel
                    : new RedirectToActionResult("Index", controller, new { area = "Admin" });
            context.ExceptionHandled = true;
        }

        /// <summary>Bizim kodun məlumatlı mesajları olduğu kimi, sistem xətaları — anlaşıqlı mətnlə.</summary>
        public static string FriendlyMessage(Exception ex) => ex switch
        {
            DbUpdateConcurrencyException =>
                "Bu məlumat artıq dəyişdirilib və ya silinib. Səhifəni yeniləyib yenidən cəhd edin.",
            DbUpdateException =>
                "Əməliyyat yerinə yetirilmədi: bu element başqa məlumatlarla əlaqəlidir (məs. ona bağlı xidmət və ya rezervasiya var).",
            BadHttpRequestException { StatusCode: 413 } =>
                "Fayl çox böyükdür.",
            IOException or UnauthorizedAccessException =>
                "Fayl yadda saxlanılmadı və ya silinmədi. Bir azdan yenidən cəhd edin.",
            // Servislərimizin öz mesajları (Azərbaycan dilində): new Exception("..."), InvalidOperationException, ArgumentException
            _ when ex.GetType() == typeof(Exception) || ex is InvalidOperationException || ex is ArgumentException || ex is KeyNotFoundException =>
                ex.Message,
            _ => "Gözlənilməz xəta baş verdi. Yenidən cəhd edin; problem davam edərsə, administratora bildirin."
        };
    }
}
