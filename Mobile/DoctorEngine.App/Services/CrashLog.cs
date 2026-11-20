namespace DoctorEngine.App.Services;

/// <summary>
/// Tutulmamış xətaları fayla yazır; tətbiq növbəti dəfə açılanda istifadəçiyə göstərilir
/// (mətni kopyalayıb göndərmək olar). Telefonda debug olmadan xətanın səbəbini tapmaq üçündür.
/// </summary>
public static class CrashLog
{
    private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "last-crash.txt");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(e.Exception, "Task");
            e.SetObserved();   // arxa plan tapşırığının xətası tətbiqi bağlamasın
        };
#if ANDROID
        // Android: UI axınındakı idarə olunan xəta — yazılır, tətbiq bağlanmır
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            Write(e.Exception, "Android");
            e.Handled = true;
        };
#endif
    }

    public static void Write(Exception? ex, string source)
    {
        if (ex == null) return;
        try
        {
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {AppInfo.Current.VersionString} " +
                       $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString} {DeviceInfo.Current.Model}\n{ex}";
            File.WriteAllText(FilePath, text);
        }
        catch { /* jurnal yazıla bilmirsə — susuruq */ }
    }

    /// <summary>Əvvəlki qəzanın mətni (varsa) — oxunduqdan sonra silinir.</summary>
    public static string? TakeLast()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var text = File.ReadAllText(FilePath);
            File.Delete(FilePath);
            return text;
        }
        catch { return null; }
    }
}
