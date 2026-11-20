namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Web Push (VAPID) açarları. Development: dotnet user-secrets ("Push:PublicKey", "Push:PrivateKey").
    /// Production: mühit dəyişənləri Push__PublicKey, Push__PrivateKey (və ya appsettings).
    /// Açarlar yoxdursa push sadəcə söndürülür — çat yenə işləyir.
    /// </summary>
    public class PushOptions
    {
        public const string Section = "Push";

        public string? PublicKey { get; set; }
        public string? PrivateKey { get; set; }

        /// <summary>Push serverləri üçün əlaqə: "mailto:..." və ya sayt ünvanı.</summary>
        public string? Subject { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
    }
}
