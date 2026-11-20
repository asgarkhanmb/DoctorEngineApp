namespace DoctorEngine.App.Services;

/// <summary>
/// Server ünvanı. Debug: lokal server (Android emulyatorunda kompüter 10.0.2.2 ünvanındadır).
/// Release: canlı sayt. Ayarlar ekranından dəyişdirilə bilər (Preferences-də saxlanır).
/// </summary>
public static class AppConfig
{
    /// <summary>Canlı API (Api/DoctorEngine.Api) — yerləşdiriləndən sonra öz domeninizlə əvəz edin.</summary>
    public const string ProductionUrl = "https://api.doctorengine.az";

    /// <summary>Lokal API portu (Api/DoctorEngine.Api/Properties/launchSettings.json → "DoctorEngine.Api" profili).</summary>
    public const int DevPort = 5020;

    private const string Key = "api_base_url";

    public static string DefaultUrl =>
#if TESTBUILD
        // Test APK: build zamanı verilən ünvan (-p:TestServerUrl=http://<kompüterin IP-si>:5020)
        typeof(AppConfig).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TestServerUrl")?.Value ?? $"http://10.0.2.2:{DevPort}";
#elif DEBUG
        DeviceInfo.Platform == DevicePlatform.Android ? $"http://10.0.2.2:{DevPort}" : $"http://localhost:{DevPort}";
#else
        ProductionUrl;
#endif

    public static string BaseUrl
    {
        get => Preferences.Default.Get(Key, DefaultUrl);
        set
        {
            var v = (value ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(v)) Preferences.Default.Remove(Key);
            else Preferences.Default.Set(Key, v);
        }
    }

    /// <summary>Canlı sayt — JS front (Server/Front). Zəng səhifəsi /Call oradadır; zəng siqnalları (/callHub) API-dədir.</summary>
    public const string ProductionSiteUrl = "https://doctorengine.az";

    /// <summary>Lokal JS front portu (API Development-də Server/Front-u burada açır).</summary>
    public const int DevSitePort = 5021;

    /// <summary>
    /// Saytın ünvanı — video / səsli zənglər üçün. Kamera WebView-da yalnız HTTPS-də (və ya localhost-da) işləyir.
    /// Test APK: -p:TestSiteUrl=https://IP:5021 (JS front; kamera üçün HTTPS lazımdır).
    /// </summary>
    public static string SiteUrl
    {
        get
        {
#if TESTBUILD
            var test = typeof(AppConfig).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "TestSiteUrl")?.Value;
            if (!string.IsNullOrWhiteSpace(test)) return test.TrimEnd('/');
#endif
            var b = BaseUrl;
            if (string.Equals(b, ProductionUrl, StringComparison.OrdinalIgnoreCase)) return ProductionSiteUrl;
            // Lokal: eyni kompüter, API portu (5020) → front portu (5021)
            return b.Replace(":" + DevPort, ":" + DevSitePort);
        }
    }

    /// <summary>Serverdən gələn nisbi ünvanı ("/uploads/...") tam ünvana çevirir.</summary>
    public static string? Absolute(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        if (relative.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return relative;
        return BaseUrl + (relative.StartsWith('/') ? relative : "/" + relative);
    }

    public static string DeviceName =>
        $"{DeviceInfo.Current.Model} ({DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString})";
}
