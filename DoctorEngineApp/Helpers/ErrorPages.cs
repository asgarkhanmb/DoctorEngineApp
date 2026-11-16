using DoctorEngineApp.Localization;
namespace DoctorEngineApp.Helpers
{
    /// <summary>Xəta səhifəsində göstərilən məlumat (bax: ErrorController, Views/Error/Status.cshtml).</summary>
    public class ErrorPageVM
    {
        public int Code { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Icon { get; init; } = "fa-solid fa-triangle-exclamation";

        /// <summary>"Yenidən cəhd et" düyməsi göstərilsin (müvəqqəti xətalar).</summary>
        public bool CanRetry { get; init; }

        /// <summary>Daxil olmaq düyməsi (401/403).</summary>
        public bool ShowLogin { get; init; }

        /// <summary>Dəstəyə müraciət üçün sorğu kodu (loglarda axtarmaq olur).</summary>
        public string? RequestId { get; set; }

        public string SiteName { get; set; } = "DoctorEngine.az";
        public string? LoginUrl { get; set; }
    }

    /// <summary>HTTP status kodlarına uyğun Azərbaycan dilində mətnlər.</summary>
    public static class ErrorPages
    {
        public static ErrorPageVM For(int code) => code switch
        {
            400 => new() { Code = 400, Title = L.T("Sorğu düzgün deyil"), Icon = "fa-solid fa-circle-question",
                           Message = L.T("Göndərilən məlumat başa düşülmədi. Səhifəni yeniləyib yenidən cəhd edin.") },
            401 => new() { Code = 401, Title = L.T("Daxil olmaq lazımdır"), Icon = "fa-solid fa-user-lock", ShowLogin = true,
                           Message = L.T("Bu səhifəyə baxmaq üçün hesabınıza daxil olun.") },
            403 => new() { Code = 403, Title = L.T("Giriş qadağandır"), Icon = "fa-solid fa-lock", ShowLogin = true,
                           Message = L.T("Bu bölməyə daxil olmaq üçün icazəniz yoxdur.") },
            404 => new() { Code = 404, Title = L.T("Səhifə tapılmadı"), Icon = "fa-solid fa-map-signs",
                           Message = L.T("Axtardığınız səhifə mövcud deyil, silinib və ya ünvanı dəyişib.") },
            405 => new() { Code = 405, Title = L.T("Əməliyyata icazə yoxdur"), Icon = "fa-solid fa-ban",
                           Message = L.T("Bu ünvan belə sorğunu qəbul etmir.") },
            408 => new() { Code = 408, Title = L.T("Sorğunun vaxtı bitdi"), Icon = "fa-solid fa-hourglass-end", CanRetry = true,
                           Message = L.T("Server cavabı gecikdi. İnternet bağlantınızı yoxlayıb yenidən cəhd edin.") },
            413 => new() { Code = 413, Title = L.T("Fayl çox böyükdür"), Icon = "fa-solid fa-file-circle-exclamation",
                           Message = L.T("Göndərdiyiniz fayl icazə verilən həcmdən böyükdür. Daha kiçik fayl seçin.") },
            429 => new() { Code = 429, Title = L.T("Çox sayda sorğu"), Icon = "fa-solid fa-gauge-high", CanRetry = true,
                           Message = L.T("Qısa müddətdə çox sorğu göndərildi. Bir neçə saniyə gözləyib yenidən cəhd edin.") },
            502 => new() { Code = 502, Title = "Server cavab vermir", Icon = "fa-solid fa-plug-circle-xmark", CanRetry = true,
                           Message = L.T("Server müvəqqəti əlçatan deyil. Bir azdan yenidən cəhd edin.") },
            503 => new() { Code = 503, Title = L.T("Texniki işlər aparılır"), Icon = "fa-solid fa-screwdriver-wrench", CanRetry = true,
                           Message = L.T("Sayt müvəqqəti olaraq əlçatan deyil. Tezliklə qayıdacağıq.") },
            504 => new() { Code = 504, Title = "Server gecikir", Icon = "fa-solid fa-hourglass-half", CanRetry = true,
                           Message = L.T("Server vaxtında cavab vermədi. Bir azdan yenidən cəhd edin.") },
            >= 500 => new() { Code = code, Title = L.T("Serverdə xəta baş verdi"), Icon = "fa-solid fa-server", CanRetry = true,
                           Message = L.T("Gözlənilməz xəta baş verdi. Artıq məlumatlıyıq — bir azdan yenidən cəhd edin.") },
            _ => new() { Code = code, Title = L.T("Xəta baş verdi"), Icon = "fa-solid fa-triangle-exclamation", CanRetry = true,
                           Message = L.T("Sorğu yerinə yetirilmədi. Yenidən cəhd edin.") }
        };

        /// <summary>API, SignalR hub-ları və statik fayllar — HTML xəta səhifəsi göstərilmir.</summary>
        public static bool IsNonPageRequest(PathString path) =>
            path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/chatHub") ||
            path.StartsWithSegments("/reservationHub") ||
            Path.HasExtension(path.Value);   // /uploads/x.jpg, /css/x.css ...
    }
}
