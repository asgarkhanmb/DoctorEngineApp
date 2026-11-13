namespace DoctorEngine.App.Views;

/// <summary>
/// Geniş ekranda (Windows / macOS pəncərəsi böyüdüləndə, planşet) məzmun ekran boyu uzanmasın:
/// səhifə ortada maksimum <see cref="MaxWidth"/> enində sütun kimi göstərilir.
/// </summary>
public static class PageLayout
{
    public const double MaxWidth = 900;

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
}
