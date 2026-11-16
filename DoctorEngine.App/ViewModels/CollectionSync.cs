using System.Collections.ObjectModel;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Siyahıları Clear() (Reset hadisəsi) olmadan yeniləyir. Windows-da (WinUI) BindableLayout Reset alanda
/// UIElementCollection.Clear() bəzən tətbiqi xəbərdarlıqsız bağlayır (0xc0000005) — xüsusən "aşağı çəkib yenilə" zamanı.
/// Burada yalnız Replace / Add / Remove hadisələri göndərilir; dəyişməyən elementlərə toxunulmur.
/// </summary>
internal static class CollectionSync
{
    public static void SyncWith<T>(this ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        var eq = EqualityComparer<T>.Default;
        for (var i = 0; i < list.Count; i++)
        {
            if (i < target.Count)
            {
                if (!eq.Equals(target[i], list[i])) target[i] = list[i];
            }
            else target.Add(list[i]);
        }
        while (target.Count > list.Count) target.RemoveAt(target.Count - 1);
    }

    /// <summary>Eyni elementləri yenidən çəkdirir (məs. dil dəyişəndə) — hər biri üçün Replace hadisəsi.</summary>
    public static void Rebind<T>(this ObservableCollection<T> target)
    {
        for (var i = 0; i < target.Count; i++) target[i] = target[i];
    }
}
