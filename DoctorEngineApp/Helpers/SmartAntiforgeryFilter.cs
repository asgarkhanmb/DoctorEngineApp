using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// AutoValidateAntiforgeryToken-in əvəzi: sayt (cookie) sorğularında CSRF tokeni əvvəlki kimi yoxlanılır,
    /// tətbiqlərdən gələn "Authorization: Bearer ..." sorğularında isə yoxlanılmır.
    /// Səbəb: CSRF yalnız brauzerin avtomatik göndərdiyi cookie ilə mümkündür; Bearer header-i
    /// brauzer heç vaxt özü əlavə etmir (başqa saytdan göndərmək üçün CORS icazəsi lazımdır — verilmir).
    /// Bearer sorğuları "Smart" autentifikasiya sxemi ilə yalnız JWT ilə yoxlanılır, cookie nəzərə alınmır.
    /// </summary>
    public class SmartAntiforgeryFilter : IAsyncAuthorizationFilter, IAntiforgeryPolicy
    {
        private readonly IAntiforgery _antiforgery;

        public SmartAntiforgeryFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;
            var method = http.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
                HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
                return;

            // [IgnoreAntiforgeryToken] və ya action-un öz [ValidateAntiForgeryToken]-i ən yaxın siyasətdir
            var closest = context.Filters.OfType<IAntiforgeryPolicy>().LastOrDefault();
            if (closest != null && !ReferenceEquals(closest, this) && closest is not SmartAntiforgeryFilter)
                return;

            if (IsBearer(http)) return;

            try
            {
                await _antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new AntiforgeryValidationFailedResult();
            }
        }

        public static bool IsBearer(HttpContext http) =>
            http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }
}
