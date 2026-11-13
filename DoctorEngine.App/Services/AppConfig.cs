namespace DoctorEngine.App.Services;

/// <summary>
/// Server ünvanı. Debug: lokal server (Android emulyatorunda kompüter 10.0.2.2 ünvanındadır).
/// Release: canlı sayt. Ayarlar ekranından dəyişdirilə bilər (Preferences-də saxlanır).
/// </summary>
public static class AppConfig
{
    /// <summary>Canlı server — sayt yerləşdiriləndən sonra öz domeninizlə əvəz edin.</summary>
    public const string ProductionUrl = "https://doctorengine.az";

    /// <summary>Lokal server portu (DoctorEngineApp/Properties/launchSettings.json → "DoctorEngineApp" profili).</summary>
    public const int DevPort = 5019;

    private const string Key = "api_base_url";

    public static string DefaultUrl =>
#if DEBUG
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
