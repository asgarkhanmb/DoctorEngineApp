using DoctorEngine.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace DoctorEngine.App.Views;

/// <summary>
/// Instagram üslubunda "üzən" alt menyu: kənarları yumru kapsul, arxası bulanıq şüşə (Windows — Acrylic,
/// Android / iOS — yarışəffaf), yalnız ikonlar (seçilməmiş — kontur, seçilmiş — dolu), profil — istifadəçinin şəkli.
/// Aşağı sürüşdürəndə bir az kiçilir, yuxarı sürüşdürəndə əvvəlki ölçüsünə qayıdır.
/// İstifadə: tab səhifəsinin konstruktorunda <c>this.UseBottomNav("home")</c>.
/// </summary>
public class BottomNav : ContentView
{
    private static readonly string[] Routes = ["home", "services", "reels", "notifications", "chat", "profile"];

    public const double BarHeight = 52;
    public const double BottomGap = 10;
    /// <summary>Üzən menyu altında qalmasın deyə sürüşən məzmunun sonuna əlavə olunan boşluq.</summary>
    public const double ContentInset = BarHeight + BottomGap + 16;

    private const double CompactScale = 0.88;

    private readonly string _active;
    private readonly bool _dark;
    private readonly Border _pill;
    private Grid? _avatarRing;
    private Image? _avatarImage;
    private Label? _avatarInitial;
    private Image? _profileIcon;
    private bool _compact;

    public BottomNav(string active, bool dark)
    {
        _active = active;
        _dark = dark;

        var row = new Grid();
        for (var i = 0; i < Routes.Length; i++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.Add(BuildItem(Routes[i]), i);
        }

        _pill = new Border
        {
            HeightRequest = BarHeight,
            StrokeShape = new RoundRectangle { CornerRadius = BarHeight / 2 },
            StrokeThickness = 0.8,
            Padding = new Thickness(6, 0),
            Content = row,
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.22f, Radius = 22, Offset = new Point(0, 6) }
        };
        // Şüşə effekti: yarışəffaf fon + nazik işıqlı kənar (Windows-da üstəlik həqiqi bulanıqlıq — Acrylic)
        Themed(_pill, Border.BackgroundProperty,
            new SolidColorBrush(Color.FromArgb("#D9FFFFFF")), new SolidColorBrush(Color.FromArgb("#C7161616")));
        Themed(_pill, Border.StrokeProperty,
            new SolidColorBrush(Color.FromArgb("#14000000")), new SolidColorBrush(Color.FromArgb("#2EFFFFFF")));
        _pill.HandlerChanged += (_, _) => Glass.Apply(_pill, _dark || Application.Current?.RequestedTheme == AppTheme.Dark);

        Content = _pill;
        Margin = new Thickness(22, 0, 22, BottomGap);
        VerticalOptions = LayoutOptions.End;
        MaximumWidthRequest = 460;

        Loaded += (_, _) => { Auth.Changed += OnAuthChanged; UpdateAvatar(); };
        Unloaded += (_, _) => Auth.Changed -= OnAuthChanged;
    }

    private static AuthService Auth => IPlatformApplication.Current!.Services.GetRequiredService<AuthService>();

    /// <summary>Sürüşdürmə istiqamətinə görə: aşağı — kiçil, yuxarı — böyü.</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact) return;
        _compact = compact;
        _pill.AbortAnimation("de-nav");
        var from = _pill.Scale;
        var to = compact ? CompactScale : 1;
        var fromY = _pill.TranslationY;
        var toY = compact ? 6 : 0;
        new Animation(v =>
        {
            _pill.Scale = from + (to - from) * v;
            _pill.TranslationY = fromY + (toY - fromY) * v;
        }).Commit(_pill, "de-nav", 16, 220, compact ? Easing.CubicOut : Easing.SpringOut);
    }

    /// <summary>Açıq / tünd mövzuya görə dəyər; Videolar səhifəsində həmişə tünd.</summary>
    private void Themed(BindableObject target, BindableProperty property, object light, object dark)
    {
        if (_dark) target.SetValue(property, dark);
        else target.SetAppTheme(property, light, dark);
    }

    private View BuildItem(string route)
    {
        var active = route == _active;
        var suffix = active ? "f" : "";
        var image = new Image
        {
            WidthRequest = 23,
            HeightRequest = 23,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Themed(image, Image.SourceProperty,
            ImageSource.FromFile($"nav_{route}_k{suffix}.png"),
            ImageSource.FromFile($"nav_{route}_w{suffix}.png"));

        var cell = new Grid { BackgroundColor = Colors.Transparent };
        cell.Add(image);

        if (route == "profile")
        {
            // Daxil olubsa — profil şəkli; seçiləndə ətrafında halqa (Instagram kimi)
            _profileIcon = image;
            _avatarImage = new Image { Aspect = Aspect.AspectFill, WidthRequest = 24, HeightRequest = 24 };
            // Şəkil yüklənməsə (oflayn) — adın ilk hərfi görünür
            _avatarInitial = new Label
            {
                FontFamily = "BarlowBold",
                FontSize = 12,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            var photo = new Border
            {
                WidthRequest = 24,
                HeightRequest = 24,
                StrokeShape = new Ellipse(),
                StrokeThickness = 0,
                Background = new LinearGradientBrush(
                    [new GradientStop(Color.FromArgb("#F0263A"), 0), new GradientStop(Color.FromArgb("#B00F1D"), 1)],
                    new Point(0, 0), new Point(1, 1)),
                Content = new Grid { _avatarInitial, _avatarImage },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            var ring = new Ellipse
            {
                WidthRequest = 29,
                HeightRequest = 29,
                StrokeThickness = active ? 1.8 : 0,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            Themed(ring, Shape.StrokeProperty, new SolidColorBrush(Color.FromArgb("#111111")), new SolidColorBrush(Colors.White));
            _avatarRing = new Grid { IsVisible = false };
            _avatarRing.Add(ring);
            _avatarRing.Add(photo);
            cell.Add(_avatarRing);
        }

        if (route == "notifications") cell.Add(BuildBadge());

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            await cell.ScaleToAsync(0.84, 60);
            _ = cell.ScaleToAsync(1, 140, Easing.SpringOut);
            // Öz bölməsinin kök səhifəsindədirsə — heç nə; alt səhifədədirsə (məs. Rezervasiya) — bölmənin əvvəlinə qayıt
            if (active && (Shell.Current?.Navigation.NavigationStack.Count ?? 1) <= 1) return;
            try { if (Shell.Current != null) await Shell.Current.GoToAsync("//" + route, false); }
            catch (Exception ex) { CrashLog.Write(ex, "BottomNav→" + route); }
        };
        cell.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(cell, route);
        return cell;
    }

    /// <summary>Oxunmamış bildirişlərin sayı (qırmızı nişan) — NotificationsViewModel.UnreadCount.</summary>
    private static View BuildBadge()
    {
        var vm = IPlatformApplication.Current!.Services.GetRequiredService<ViewModels.NotificationsViewModel>();
        var label = new Label
        {
            FontFamily = "BarlowBold",
            FontSize = 9.5,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        label.SetBinding(Label.TextProperty, static (ViewModels.NotificationsViewModel v) => v.UnreadText);
        var badge = new Border
        {
            MinimumWidthRequest = 16,
            HeightRequest = 16,
            Padding = new Thickness(4, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#FF3040"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TranslationX = 10,
            TranslationY = -9,
            InputTransparent = true,
            Content = label,
            BindingContext = vm
        };
        badge.SetBinding(IsVisibleProperty, static (ViewModels.NotificationsViewModel v) => v.HasUnread);
        return badge;
    }

    private void OnAuthChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(UpdateAvatar);

    private void UpdateAvatar()
    {
        if (_avatarRing == null || _avatarImage == null || _profileIcon == null) return;
        var url = AppConfig.Absolute(Auth.User?.AvatarUrl);
        var has = !string.IsNullOrEmpty(url);
        _avatarRing.IsVisible = has;
        _profileIcon.IsVisible = !has;
        if (_avatarInitial != null)
        {
            var name = Auth.User?.FullName;
            _avatarInitial.Text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        }
        if (has) _avatarImage.Source = url;
    }

    /// <summary>Həqiqi arxa fon bulanıqlığı (platformaya görə).</summary>
    private static class Glass
    {
        public static void Apply(Border border, bool dark)
        {
#if WINDOWS
            // WinUI Acrylic — arxadakı məzmunu bulanıqlaşdırır. MAUI Border fonu daxili Path-in Fill-i ilə çəkir.
            if (border.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement fe) return;
            void SetFill()
            {
                var acrylic = new Microsoft.UI.Xaml.Media.AcrylicBrush
                {
                    TintColor = dark ? Microsoft.UI.ColorHelper.FromArgb(255, 18, 18, 18) : Microsoft.UI.Colors.White,
                    TintOpacity = dark ? 0.55 : 0.6,
                    TintLuminosityOpacity = dark ? 0.7 : 0.75,
                    FallbackColor = dark ? Microsoft.UI.ColorHelper.FromArgb(230, 22, 22, 22) : Microsoft.UI.ColorHelper.FromArgb(235, 255, 255, 255)
                };
                if (fe is Microsoft.UI.Xaml.Controls.Panel panel)
                    foreach (var child in panel.Children)
                        if (child is Microsoft.UI.Xaml.Shapes.Path path) { path.Fill = acrylic; return; }
            }
            fe.Loaded += (_, _) => SetFill();
            SetFill();
#endif
        }
    }
}

public static class BottomNavExtensions
{
    /// <summary>
    /// Səhifəyə üzən alt menyu əlavə edir və Shell-in tab panelini gizlədir.
    /// floating: true — menyu məzmunun üzərində üzür (arxası bulanıq görünür), sürüşən siyahıların sonuna boşluq əlavə olunur;
    /// false — menyu məzmunun altında öz zolağındadır (Çat — yazı sahəsi örtülməsin, Videolar).
    /// </summary>
    public static void UseBottomNav(this ContentPage page, string route, bool dark = false, bool floating = true)
    {
        Shell.SetTabBarIsVisible(page, false);
        var content = page.Content;
        page.Content = null;
        var nav = new BottomNav(route, dark);

        var grid = new Grid { RowSpacing = 0 };
        if (floating)
        {
            if (content != null) grid.Add(content);
            grid.Add(nav);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            if (content != null) grid.Add(content, 0, 0);
            nav.Margin = new Thickness(22, 6, 22, BottomNav.BottomGap);
            grid.Add(nav, 0, 1);
            // Menyu zolağının fonu — səhifənin fonu ilə eyni
            if (content is VisualElement ve && ve.BackgroundColor != null) grid.BackgroundColor = ve.BackgroundColor;
            else if (dark) grid.BackgroundColor = Colors.Black;
            else grid.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#F4F6FA"), Color.FromArgb("#0E1424"));
        }
        page.Content = grid;

        // Sürüşdürmə: aşağı — menyu kiçilir, yuxarı — böyüyür. Üzən menyuda siyahının sonu altında qalmasın.
        var hooked = false;
        page.Loaded += (_, _) =>
        {
            if (hooked || content == null) return;
            hooked = true;
            foreach (var el in Descendants(content))
            {
                if (el is ScrollView sv && sv.Orientation != ScrollOrientation.Horizontal)
                {
                    if (floating) sv.Padding = new Thickness(sv.Padding.Left, sv.Padding.Top, sv.Padding.Right, sv.Padding.Bottom + BottomNav.ContentInset);
                    double last = 0;
                    sv.Scrolled += (_, e) =>
                    {
                        var dy = e.ScrollY - last;
                        last = e.ScrollY;
                        OnScroll(nav, dy, e.ScrollY);
                    };
                }
                else if (el is CollectionView cv && cv.ItemsLayout is not LinearItemsLayout { Orientation: ItemsLayoutOrientation.Horizontal }
                                                   && cv.ItemsLayout is not GridItemsLayout { Orientation: ItemsLayoutOrientation.Horizontal })
                {
                    if (floating)
                    {
                        if (cv.Footer is View footer) footer.Margin = new Thickness(footer.Margin.Left, footer.Margin.Top, footer.Margin.Right, footer.Margin.Bottom + BottomNav.ContentInset);
                        else if (cv.Footer == null) cv.Footer = new BoxView { HeightRequest = BottomNav.ContentInset, Color = Colors.Transparent };
                    }
                    cv.Scrolled += (_, e) => OnScroll(nav, e.VerticalDelta, e.VerticalOffset);
                }
            }
        };
    }

    private static void OnScroll(BottomNav nav, double dy, double offset)
    {
        if (offset <= 8) nav.SetCompact(false);          // ən yuxarıda — həmişə tam ölçü
        else if (dy > 4) nav.SetCompact(true);           // aşağı
        else if (dy < -4) nav.SetCompact(false);         // yuxarı
    }

    private static IEnumerable<Element> Descendants(Element root)
    {
        if (root is not IVisualTreeElement vt) yield break;
        foreach (var child in vt.GetVisualChildren())
        {
            if (child is not Element e) continue;
            yield return e;
            foreach (var d in Descendants(e)) yield return d;
        }
    }
}
