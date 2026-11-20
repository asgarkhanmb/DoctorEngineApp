namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// AI köməkçi ayarları (appsettings.json → "Ai").
    /// İstənilən OpenAI-uyğun API ilə işləyir. Standart: Google Gemini pulsuz versiyası.
    /// API açarı sayt sahibi tərəfindən BİR DƏFƏ verilir (müştərilərdən heç nə tələb olunmur):
    ///   dotnet user-secrets set "Ai:ApiKey" "AIza..."
    /// </summary>
    public class AiOptions
    {
        public const string Section = "Ai";

        /// <summary>OpenAI-uyğun endpoint (sonunda "/" olmalıdır).</summary>
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

        /// <summary>Model adı. Gemini pulsuz versiyası: gemini-3.8-flash (daha qənaətli: gemini-3.5-flash-lite).</summary>
        public string Model { get; set; } = "gemini-3.8-flash";

        public string? ApiKey { get; set; }

        public int MaxOutputTokens { get; set; } = 900;
        public double Temperature { get; set; } = 0.4;

        /// <summary>Hər istifadəçi (brauzer) üçün gündəlik sual limiti. Bakı vaxtı ilə gecə yarısı sıfırlanır.</summary>
        public int DailyPerUser { get; set; } = 20;

        /// <summary>
        /// Bir IP ünvanı üçün gündəlik limit — kukini silib limiti aşmağın qarşısı.
        /// İstifadəçi limitindən böyükdür, çünki bir ofis/ev şəbəkəsində bir neçə nəfər ola bilər.
        /// </summary>
        public int DailyPerIp { get; set; } = 60;

        /// <summary>Qısa müddətli qorunma: bir istifadəçi 1 dəqiqədə maksimum neçə sual verə bilər.</summary>
        public int PerMinute { get; set; } = 5;

        /// <summary>Bütün sayt üzrə gündəlik maksimum (pulsuz API limitini aşmamaq üçün).</summary>
        public int DailyLimit { get; set; } = 900;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
