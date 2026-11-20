namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Tətbiqlər üçün JWT ayarları. Açar: dotnet user-secrets set "Jwt:Key" "..." (development),
    /// canlı serverdə mühit dəyişəni Jwt__Key. Açar yoxdursa API girişi söndürülür (sayt işləməyə davam edir).
    /// </summary>
    public class JwtOptions
    {
        public const string Section = "Jwt";

        public string? Key { get; set; }
        public string Issuer { get; set; } = "DoctorEngine";
        public string Audience { get; set; } = "DoctorEngine.Apps";

        /// <summary>Access token ömrü — qısa: oğurlansa da tez yararsız olur.</summary>
        public int AccessTokenMinutes { get; set; } = 30;

        /// <summary>Refresh token ömrü — tətbiqdə yenidən giriş etmədən qalma müddəti.</summary>
        public int RefreshTokenDays { get; set; } = 60;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Key) && Key.Length >= 32;
    }
}
