namespace DoctorEngine.App.Views;

/// <summary>
/// "Canlı" interfeys üçün kiçik animasiyalar:
///  • Reveal — səhifə açılanda bölmələr növbə ilə aşağıdan yuxarı üzə çıxır (ClassId="reveal");
///  • Press — toxunanda element yüngülcə sıxılır (attached: Motion.Press="True");
///  • Pulse / Shimmer — "canlı" nöqtə və skelet yüklənmə üçün sonsuz animasiya;
///  • Appear — siyahı elementləri dalğa ilə üzə çıxır; CountTo — rəqəmlər sayaraq böyüyür;
///  • Enter — tab dəyişəndə səhifə yumşaq daxil olur; Float / Drift — başlıqda dekorativ hərəkət;
///  • düymələr (Button) basanda yayla sıxılır (AttachButtonPress, MauiProgram-da qoşulur).
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

    /// <summary>
    /// Bütün düymələr (Button) üçün: basanda yüngülcə sıxılır, buraxanda yayla qayıdır.
    /// MauiProgram-da ButtonHandler mapper-i ilə hər düyməyə avtomatik qoşulur.
    /// </summary>
    public static void AttachButtonPress(Button b)
    {
        if (b.GetValue(ButtonPressAttachedProperty) is true) return;
        b.SetValue(ButtonPressAttachedProperty, true);
        b.Pressed += async (_, _) => { try { await b.ScaleToAsync(0.96, 80, Easing.CubicOut); } catch { } };
        b.Released += async (_, _) => { try { await b.ScaleToAsync(1, 260, Easing.SpringOut); } catch { } };
    }

    private static readonly BindableProperty ButtonPressAttachedProperty = BindableProperty.CreateAttached(
        "ButtonPressAttached", typeof(bool), typeof(Motion), false);

    // ---------- Appear (siyahı elementləri) ----------

    /// <summary>
    /// Element ekrana gələndə (Loaded) aşağıdan yuxarı üzə çıxır. Eyni anda yüklənən elementlər
    /// dalğa kimi növbə ilə görünür (kartlar, rezervasiyalar, mesajlar). XAML: v:Motion.Appear="True".
    /// </summary>
    public static readonly BindableProperty AppearProperty = BindableProperty.CreateAttached(
        "Appear", typeof(bool), typeof(Motion), false, propertyChanged: OnAppearChanged);

    public static bool GetAppear(BindableObject b) => (bool)b.GetValue(AppearProperty);
    public static void SetAppear(BindableObject b, bool v) => b.SetValue(AppearProperty, v);

    private static DateTime _lastAppear;
    private static int _appearIndex;

    private static void OnAppearChanged(BindableObject b, object o, object n)
    {
        if (b is not VisualElement v || n is not true) return;
        v.Opacity = 0;
        v.TranslationY = 18;
        v.Loaded += OnAppearLoaded;
    }

    private static void OnAppearLoaded(object? sender, EventArgs e)
    {
        if (sender is not VisualElement v) return;
        v.Loaded -= OnAppearLoaded;

        // Qısa fasilə ilə gələn elementlər növbəyə düzülür (maks. 8 addım — uzun siyahı gözlətməsin)
        var now = DateTime.UtcNow;
        _appearIndex = (now - _lastAppear).TotalMilliseconds < 140 ? Math.Min(_appearIndex + 1, 8) : 0;
        _lastAppear = now;

        v.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(_appearIndex * 55), async () =>
        {
            try
            {
                await Task.WhenAll(
                    v.FadeToAsync(1, 320, Easing.CubicOut),
                    v.TranslateToAsync(0, 0, 420, Easing.CubicOut));
            }
            catch { v.Opacity = 1; v.TranslationY = 0; }
        });
    }

    // ---------- CountTo (rəqəmlər sayaraq böyüyür) ----------

    /// <summary>Label-in mətni əvvəlki rəqəmdən yenisinə sayaraq dəyişir. XAML: v:Motion.CountTo="{Binding Say}".</summary>
    public static readonly BindableProperty CountToProperty = BindableProperty.CreateAttached(
        "CountTo", typeof(int?), typeof(Motion), null, propertyChanged: OnCountToChanged);

    public static int? GetCountTo(BindableObject b) => (int?)b.GetValue(CountToProperty);
    public static void SetCountTo(BindableObject b, int? v) => b.SetValue(CountToProperty, v);

    private static void OnCountToChanged(BindableObject b, object o, object n)
    {
        if (b is not Label label) return;
        if (n is not int to) { label.Text = "–"; return; }
        var from = o as int? ?? 0;
        label.AbortAnimation("de-count");
        if (from == to) { label.Text = to.ToString(); return; }
        new Animation(t => label.Text = ((int)Math.Round(from + (to - from) * t)).ToString(), 0, 1, Easing.CubicOut)
            .Commit(label, "de-count", length: (uint)Math.Clamp(350 + Math.Abs(to - from) * 25, 350, 900),
                    finished: (_, _) => label.Text = to.ToString());
    }

    // ---------- Səhifəyə giriş ----------

    /// <summary>Tab dəyişəndə səhifənin məzmunu yumşaq daxil olur.</summary>
    public static void Enter(ContentPage page)
    {
        if (page.Content is not View c) return;
        c.AbortAnimation("de-enter");
        c.Opacity = 0.35;
        c.TranslationY = 10;
        new Animation
        {
            { 0, 1, new Animation(t => c.Opacity = t, 0.35, 1, Easing.CubicOut) },
            { 0, 1, new Animation(t => c.TranslationY = t, 10, 0, Easing.CubicOut) }
        }.Commit(c, "de-enter", length: 260, finished: (_, _) => { c.Opacity = 1; c.TranslationY = 0; });
    }

    // ---------- Float / Drift (dekorativ) ----------

    /// <summary>Yüngül "üzmə" — başlıq ikonları üçün.</summary>
    public static void Float(VisualElement v, double amplitude = 3, uint period = 2800) =>
        new Animation
        {
            { 0, 0.5, new Animation(t => v.TranslationY = t, 0, -amplitude, Easing.SinInOut) },
            { 0.5, 1, new Animation(t => v.TranslationY = t, -amplitude, 0, Easing.SinInOut) }
        }.Commit(v, "de-float", length: period, repeat: () => true);

    /// <summary>Başlıqdakı işıq ləkəsinin yavaş hərəkəti.</summary>
    public static void Drift(VisualElement v, double dx = 40, double dy = 14, uint period = 9000) =>
        new Animation
        {
            { 0, 0.5, new Animation(t => { v.TranslationX = dx * t; v.TranslationY = dy * t; v.Scale = 1 + 0.12 * t; }, 0, 1, Easing.SinInOut) },
            { 0.5, 1, new Animation(t => { v.TranslationX = dx * t; v.TranslationY = dy * t; v.Scale = 1 + 0.12 * t; }, 1, 0, Easing.SinInOut) }
        }.Commit(v, "de-drift", length: period, repeat: () => true);

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
