using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace DoctorEngine.Api.Infrastructure
{
    /// <summary>Saytın ünvanı (email təsdiqi, şifrə bərpası linkləri saytın səhifələrinə aparır).</summary>
    public class SiteOptions
    {
        public const string Section = "Site";

        /// <summary>Məs.: https://doctorengine.az — boşdursa API-nin öz ünvanı götürülür (yalnız lokal test üçün).</summary>
        public string? BaseUrl { get; set; }
    }

    public static class SiteLinks
    {
        /// <summary>
        /// Saytdakı səhifəyə tam link: this.SiteLink("Account/ConfirmEmail", new { userId, token })
        /// → https://doctorengine.az/Account/ConfirmEmail?userId=...&amp;token=...
        /// </summary>
        public static string SiteLink(this ControllerBase controller, string path, object values)
        {
            var request = controller.HttpContext.Request;
            var baseUrl = controller.HttpContext.RequestServices.GetService<IConfiguration>()?[$"{SiteOptions.Section}:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = $"{request.Scheme}://{request.Host}";

            var query = QueryString.Create(new RouteValueDictionary(values)
                .Where(kv => kv.Value != null)
                .Select(kv => new KeyValuePair<string, string?>(kv.Key, Convert.ToString(kv.Value))));

            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/') + query.ToUriComponent();
        }
    }
}
