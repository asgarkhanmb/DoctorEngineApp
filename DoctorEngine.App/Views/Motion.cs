namespace DoctorEngine.App.Views;

/// <summary>
/// "Canlı" interfeys üçün kiçik animasiyalar:
///  • Reveal — səhifə açılanda bölmələr növbə ilə aşağıdan yuxarı üzə çıxır (ClassId="reveal");
///  • Press — toxunanda element yüngülcə sıxılır (attached: Motion.Press="True");
///  • Pulse / Shimmer — "canlı" nöqtə və skelet yüklənmə üçün sonsuz animasiya.
/// </summary>
public static class Motion
{
    // ---------- Reveal ----------

    /// <summary>Səhifədə ClassId="reveal" olan elementləri növbə ilə göstərir (yalnız ilk açılışda).</summary>
    public static void RevealSections(Element root, uint stagger = 70)
    {
        var items = Descendants(root).OfType<VisualElement>().Where(v => v.ClassId == "reveal").ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var v = items[i];
            v.Opacity = 0;
            v.TranslationY = 26;
            var delay = (uint)(i * stagger);
            v.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), () =>
            {
                v.FadeToAsync(1, 380, Easing.CubicOut);
                v.TranslateToAsync(0, 0, 420, Easing.CubicOut);
            });
        }
    }

    private static IEnumerable<Element> Descendants(Element e)
    {
        foreach (var child in ((IElementController)e).LogicalChildren)
        {
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    // ---------- Press ----------

    public static readonly BindableProperty PressProperty = BindableProperty.CreateAttached(
        "Press", typeof(bool), typeof(Motion), false, propertyChanged: OnPressChanged);

    public static bool GetPress(BindableObject b) => (bool)b.GetValue(PressProperty);
    public static void SetPress(BindableObject b, bool v) => b.SetValue(PressProperty, v);

    private static void OnPressChanged(BindableObject b, object o, object n)
    {
        if (b is not View view || n is not true) return;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            try
            {
                await view.ScaleToAsync(0.95, 70, Easing.CubicOut);
                await view.ScaleToAsync(1, 140, Easing.SpringOut);
            }
            catch { }
        };
        // Öz əmrini daşıyan TapGestureRecognizer-lərdən əvvəl əlavə olunur — ikisi də işləyir
        view.GestureRecognizers.Insert(0, tap);
    }

    // ---------- Pulse / Shimmer ----------

    /// <summary>Sonsuz döyünmə (məs. "İndi açıqdır" nöqtəsi).</summary>
    public static void Pulse(VisualElement v, double min = 0.35, uint period = 1100)
    {
        new Animation(t => v.Opacity = t, 1, min, Easing.SinInOut)
            .Commit(v, "de-pulse", length: period, repeat: () => true,
                    finished: (_, _) => v.Opacity = 1);
        new Animation(t => v.Scale = t, 1, 1.35, Easing.SinInOut)
            .Commit(v, "de-pulse-scale", length: period, repeat: () => true);
    }

    /// <summary>Skelet qutularının yumşaq parıldaması.</summary>
    public static void Shimmer(VisualElement v) =>
        new Animation
        {
            { 0, 0.5, new Animation(t => v.Opacity = t, 0.45, 1, Easing.SinInOut) },
            { 0.5, 1, new Animation(t => v.Opacity = t, 1, 0.45, Easing.SinInOut) }
        }.Commit(v, "de-shimmer", length: 1300, repeat: () => true);
}
