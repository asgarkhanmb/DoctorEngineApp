namespace DoctorEngine.App.Views;

/// <summary>
/// Geniş ekranda (Windows / macOS pəncərəsi böyüdüləndə, planşet) məzmun ekran boyu uzanmasın:
/// səhifə ortada maksimum <see cref="MaxWidth"/> enində sütun kimi göstərilir.
/// </summary>
public static class PageLayout
{
    public const double MaxWidth = 900;

    /// <summary>Başlıq gradientinin yuxarı rəngi — telefonda status bar zonası bu rəngdə görünür.</summary>
    public static readonly Color HeaderTop = Color.FromArgb("#0A1C47");

    public static void CenterOnWideScreens(this ContentPage page)
    {
        page.SizeChanged += (_, _) =>
        {
            var side = Math.Max(0, (page.Width - MaxWidth) / 2);
            var p = page.Padding;
            if (Math.Abs(p.Left - side) > 0.5)
                page.Padding = new Thickness(side, p.Top, side, p.Bottom);
        };
    }

    /// <summary>
    /// Telefonda (Android / iOS) səhifə fonu başlığın göy rəngidir: məzmun status bar-dan aşağıda başlayır
    /// (SafeAreaEdges="Container"), status bar zonası isə başlıqla bir rəngdə görünür. Desktop-da adi fon.
    /// </summary>
    public static void UseHeaderStatusBar(this ContentPage page)
    {
        if (DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet)
            page.BackgroundColor = HeaderTop;
    }
}
