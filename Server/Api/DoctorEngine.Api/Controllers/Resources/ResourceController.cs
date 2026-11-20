using DoctorEngine.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DoctorEngine.Api.Controllers.Resources
{
    /// <summary>
    /// Saytın (front) istifadə etdiyi REST resursları üçün baza.
    /// Oxuma (GET) adətən hamıya açıqdır, yazma isə yalnız Admin (Bearer token).
    /// Formalar multipart/form-data ilə gəlir — sahə adları saytdakı VM-lərlə eynidir (fayllar da daxil).
    /// Servislərin istifadəçiyə göstərilən xətaları (adi Exception) → 400 { error, code }.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [IgnoreAntiforgeryToken]   // yalnız Bearer — CSRF riski yoxdur
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [ServiceErrorFilter]
    public abstract class ResourceController : ControllerBase
    {
        protected ObjectResult Error(int code, string message) => StatusCode(code, new ApiError(message, code));

        protected ActionResult<T> OkOrNotFound<T>(T? value) where T : class =>
            value == null ? Error(404, "Qeyd tapılmadı.") : Ok(value);
    }

    /// <summary>
    /// Servis "throw new Exception(mesaj)" edirsə — bu, istifadəçi üçün mesajdır (məs. "Kateqoriya tapılmadı").
    /// Baza xətaları (əlaqəli element, eyni vaxtda dəyişiklik) — 409 ilə anlaşıqlı mesaj.
    /// </summary>
    public sealed class ServiceErrorFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            var (code, message) = context.Exception switch
            {
                // Saytın admin panelindəki mesajlarla eyni
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                    (409, "Bu məlumat artıq dəyişdirilib və ya silinib. Səhifəni yeniləyib yenidən cəhd edin."),
                Microsoft.EntityFrameworkCore.DbUpdateException =>
                    (409, "Əməliyyat yerinə yetirilmədi: bu element başqa məlumatlarla əlaqəlidir (məs. ona bağlı xidmət və ya rezervasiya var)."),
                // Servislərin öz mesajları: new Exception("..."), InvalidOperationException, ArgumentException
                _ when context.Exception.GetType() == typeof(Exception) || context.Exception is InvalidOperationException
                       || context.Exception is ArgumentException || context.Exception is KeyNotFoundException =>
                    (400, context.Exception.Message),
                // id ilə tapılmayan element (servislər null ilə işləyib NullReferenceException atır)
                NullReferenceException when context.RouteData.Values.ContainsKey("id") =>
                    (404, "Element tapılmadı — artıq silinmiş ola bilər. Siyahını yeniləyin."),
                _ => (0, "")
            };
            if (code == 0) return;   // gözlənilməz xəta → 500 (loga yazılır)
            context.Result = new ObjectResult(new ApiError(message, code)) { StatusCode = code };
            context.ExceptionHandled = true;
        }
    }
}
