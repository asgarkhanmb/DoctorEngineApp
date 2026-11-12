using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Admin panelində uğurlu POST əməliyyatından (yarat / yadda saxla / sil ...) sonra
    /// yönləndirmə baş verəndə avtomatik bildiriş (toast) mesajı təyin edir.
    /// Controller özü TempData["Success"] / ["Error"] yazıbsa, ona toxunmur.
    /// </summary>
    public class AdminFlashFilter : IResultFilter
    {
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public AdminFlashFilter(ITempDataDictionaryFactory tempDataFactory)
        {
            _tempDataFactory = tempDataFactory;
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            var http = context.HttpContext;
            if (!HttpMethods.IsPost(http.Request.Method)) return;
            if (!string.Equals(context.RouteData.Values["area"]?.ToString(), "Admin", StringComparison.OrdinalIgnoreCase)) return;
            if (context.Result is not RedirectToActionResult) return;
            if (!context.ModelState.IsValid) return;

            var tempData = _tempDataFactory.GetTempData(http);
            if (tempData.ContainsKey("Success") || tempData.ContainsKey("Error")) return;

            var action = context.RouteData.Values["action"]?.ToString() ?? "";
            tempData["Success"] = action switch
            {
                "Create" => "Uğurla əlavə olundu.",
                "Edit" or "Update" or "Index" => "Dəyişikliklər yadda saxlanıldı.",
                "Delete" or "DeleteConfirmed" => "Uğurla silindi.",
                "Confirm" => "Rezervasiya təsdiqləndi.",
                "Reject" => "Rezervasiya rədd edildi.",
                "AddRole" => "Rol əlavə olundu.",
                "RemoveRole" => "Rol silindi.",
                _ => "Əməliyyat uğurla yerinə yetirildi."
            };
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}
