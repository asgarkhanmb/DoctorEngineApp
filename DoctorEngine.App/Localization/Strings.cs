namespace DoctorEngine.App.Localization;

/// <summary>
/// Tətbiqin bütün mətnləri — hər dil ayrı faylda (Strings.Az.cs, Strings.En.cs, Strings.Ru.cs, Strings.Tr.cs).
/// Yeni mətn əlavə edəndə açarı 4 faylın hamısına yazın. Açarlarda nöqtə olmamalıdır (XAML bağlama yolu).
/// {0}, {1} — string.Format yer tutanlarıdır.
/// </summary>
internal static partial class Strings
{
    public static IReadOnlyDictionary<string, string> For(string code) => code switch
    {
        "en" => En,
        "ru" => Ru,
        "tr" => Tr,
        _ => Az
    };
}
