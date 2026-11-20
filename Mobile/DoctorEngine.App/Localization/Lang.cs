using System.ComponentModel;
using System.Globalization;

namespace DoctorEngine.App.Localization;

/// <summary>Dəstəklənən dil: kod, qısa ad (düymədə), öz dilində adı və formatlama mədəniyyəti.</summary>
public sealed record LanguageInfo(string Code, string Short, string NativeName, string CultureName);

/// <summary>
/// Tətbiqin dili (az / en / ru / tr). Seçim cihazda yadda saxlanır; ilk açılışda cihazın dili
/// dəstəklənirsə o, yoxsa Azərbaycan dili götürülür.
/// XAML: <c>Text="{l:Tr açar}"</c> — dil dəyişəndə mətnlər dərhal yenilənir (tətbiqi yenidən açmadan).
/// C#: <c>Lang.T("açar")</c> və ya <c>Lang.T("açar", arg0, ...)</c> (string.Format).
/// </summary>
public sealed class Lang : INotifyPropertyChanged
{
    public static readonly IReadOnlyList<LanguageInfo> Supported = new[]
    {
        new LanguageInfo("az", "AZ", "Azərbaycanca", "az-Latn-AZ"),
        new LanguageInfo("en", "EN", "English", "en-US"),
        new LanguageInfo("ru", "RU", "Русский", "ru-RU"),
        new LanguageInfo("tr", "TR", "Türkçe", "tr-TR"),
    };

    private const string PrefKey = "app_language";

    public static Lang Current { get; } = new();

    private readonly WeakEventManager _changed = new();
    private IReadOnlyDictionary<string, string> _strings = Strings.Az;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Dil dəyişdi. Abunəçilər zəif istinadla saxlanır (ViewModel-lər yaddaşda ilişib qalmır).</summary>
    public event EventHandler Changed
    {
        add => _changed.AddEventHandler(value);
        remove => _changed.RemoveEventHandler(value);
    }

    public string Code { get; private set; } = "az";

    /// <summary>Tarix / ay adları üçün mədəniyyət (məs. "15 noyabr", "15 ноября").</summary>
    public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

    private Lang()
    {
        string? saved = null;
        try { saved = Preferences.Default.Get<string?>(PrefKey, null); } catch { }
        Apply(saved ?? DeviceDefault());
    }

    public string this[string key] => Get(key);

    public static string T(string key) => Current.Get(key);

    public static string T(string key, params object?[] args) =>
        string.Format(Current.Culture, Current.Get(key), args);

    public void Set(string code)
    {
        if (code == Code) return;
        Apply(code);
        try { Preferences.Default.Set(PrefKey, Code); } catch { }
        // null — bütün indeksator bağlamaları ({l:Tr ...}) yenilənir
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        _changed.HandleEvent(this, EventArgs.Empty, nameof(Changed));
    }

    /// <summary>Tərcümə yoxdursa — Azərbaycan dilindəki mətn, o da yoxdursa açarın özü.</summary>
    private string Get(string key) =>
        _strings.TryGetValue(key, out var s) ? s
        : Strings.Az.TryGetValue(key, out var az) ? az
        : key;

    private void Apply(string code)
    {
        var info = Supported.FirstOrDefault(l => l.Code == code) ?? Supported[0];
        Code = info.Code;
        _strings = Strings.For(info.Code);
        try { Culture = new CultureInfo(info.CultureName); }
        catch (CultureNotFoundException) { Culture = CultureInfo.InvariantCulture; }
        // Yalnız UI mədəniyyəti — rəqəm / tarix parse-i (CurrentCulture) dəyişmir
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    private static string DeviceDefault()
    {
        var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Supported.Any(l => l.Code == device) ? device : "az";
    }
}
