using System.Globalization;

namespace DoctorEngine.App.Converters;

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>null / boş sətir → false. Parametr "inv" — tərsinə (boşdursa true).</summary>
public class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value is string s ? !string.IsNullOrWhiteSpace(s) : value != null;
        return parameter as string == "inv" ? !has : has;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Rezervasiya statusu → Azərbaycan dilində mətn.</summary>
public class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as string) switch
    {
        "Pending" => "Gözləyir",
        "Confirmed" => "Təsdiqləndi",
        "Rejected" => "Rədd edildi",
        _ => value?.ToString() ?? ""
    };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Rezervasiya statusu → rəng (parametr "bg" — fon rəngi).</summary>
public class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bg = parameter as string == "bg";
        return (value as string) switch
        {
            "Confirmed" => Color.FromArgb(bg ? "#E9F8EF" : "#15803D"),
            "Rejected" => Color.FromArgb(bg ? "#FDECEE" : "#B00F1D"),
            _ => Color.FromArgb(bg ? "#FFF6E5" : "#B45309")
        };
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Öz mesajım → sağda, başqasınınkı → solda.</summary>
public class MineAlignConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? LayoutOptions.End : LayoutOptions.Start;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Mesaj qabarcığının fonu: öz — qırmızı, usta — açıq çəhrayı, digər — ağ.</summary>
public class BubbleColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ViewModels.ChatItem m) return Colors.White;
        if (m.IsMine) return Color.FromArgb("#D81324");
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (m.IsAdmin) return Color.FromArgb(dark ? "#3A1E26" : "#FFF7F7");
        return Color.FromArgb(dark ? "#182036" : "#FFFFFF");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Öz mesajımda mətn ağ, digərlərində adi rəng.</summary>
public class BubbleTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true) return Colors.White;
        return Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#EEF1F7") : Color.FromArgb("#1D2433");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Saat düyməsi: seçilmiş / boş / tutulub / keçib.</summary>
public class SlotBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ViewModels.SlotItem s) return Colors.Transparent;
        if (s.IsSelected) return Color.FromArgb("#0B2154");
        return s.State switch
        {
            "free" => Color.FromArgb("#FFFFFF"),
            "busy" => Color.FromArgb("#FDECEE"),
            _ => Color.FromArgb("#EEF0F4")
        };
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class SlotTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ViewModels.SlotItem s) return Colors.Black;
        if (s.IsSelected) return Colors.White;
        return s.State switch
        {
            "free" => Color.FromArgb("#0B2154"),
            "busy" => Color.FromArgb("#D81324"),
            _ => Color.FromArgb("#A3AAB8")
        };
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Gün düyməsi seçilib → tünd göy fon.</summary>
public class SelectedBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#D81324" : "#0B2154")   // tünd fonda göy seçim seçilmir — qırmızı
            : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#182036") : Colors.White);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class SelectedTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Colors.White
            : Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#EEF1F7" : "#0B2154");
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Mətni Azərbaycan dili qaydası ilə böyük hərfə çevirir (i → İ, ı → I).</summary>
public class UpperAzConverter : IValueConverter
{
    private static readonly CultureInfo Az = new("az-Latn-AZ");
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value?.ToString() ?? "").ToUpper(Az);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}