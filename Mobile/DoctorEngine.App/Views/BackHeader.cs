using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace DoctorEngine.App.Views;

/// <summary>
/// Alt səhifələrin başlığı (Profil redaktəsi, Admin panel ekranları): geri düyməsi + ikon + başlıq,
/// istəyə görə sağda düymə (məs. "+ Yeni"). AI səhifəsinin başlığı ilə eyni görünüş.
/// </summary>
public class BackHeader : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(BackHeader), "", propertyChanged: (b, _, n) => ((BackHeader)b)._title.Text = n as string);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(BackHeader), null, propertyChanged: (b, _, n) =>
        {
            var h = (BackHeader)b;
            h._subtitle.Text = n as string;
            h._subtitle.IsVisible = !string.IsNullOrEmpty(n as string);
        });

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(BackHeader), "ic_user_w.png", propertyChanged: (b, _, n) => ((BackHeader)b)._icon.Source = n as string);

    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(
        nameof(ActionText), typeof(string), typeof(BackHeader), null, propertyChanged: (b, _, n) =>
        {
            var h = (BackHeader)b;
            h._actionLabel.Text = n as string;
            h.UpdateAction();
        });

    public static readonly BindableProperty ActionCommandProperty = BindableProperty.Create(
        nameof(ActionCommand), typeof(ICommand), typeof(BackHeader), null, propertyChanged: (b, _, n) =>
        {
            var h = (BackHeader)b;
            h._actionTap.Command = n as ICommand;
            h.UpdateAction();
        });

    public static readonly BindableProperty ActionVisibleProperty = BindableProperty.Create(
        nameof(ActionVisible), typeof(bool), typeof(BackHeader), true, propertyChanged: (b, _, _) => ((BackHeader)b).UpdateAction());

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => (string?)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? ActionText { get => (string?)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
    public bool ActionVisible { get => (bool)GetValue(ActionVisibleProperty); set => SetValue(ActionVisibleProperty, value); }

    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Image _icon;
    private readonly Border _action;
    private readonly Label _actionLabel;
    private readonly TapGestureRecognizer _actionTap = new();

    public BackHeader()
    {
        var back = new Border { Style = Res<Style>("GlassIconButton"), Content = new Image { Source = "ic_back_w.png" } };
        back.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(GoBack) });
        Motion.SetPress(back, true);

        _icon = new Image { Source = Icon };
        var iconBox = new Border
        {
            WidthRequest = 44, HeightRequest = 44, Padding = 11, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Background = Res<Brush>("PrimaryGradient"),
            Content = _icon
        };

        _title = new Label { TextColor = Colors.White, FontFamily = "BarlowBold", FontSize = 19, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        _subtitle = new Label { TextColor = Res<Color>("OnHeaderMuted"), FontSize = 12, IsVisible = false, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
        var texts = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 0, Children = { _title, _subtitle } };

        _actionLabel = new Label { TextColor = Colors.White, FontFamily = "BarlowSemiBold", FontSize = 13 };
        _action = new Border
        {
            Style = Res<Style>("Pill"), BackgroundColor = Res<Color>("Glass"),
            VerticalOptions = LayoutOptions.Center, IsVisible = false, Content = _actionLabel
        };
        _action.GestureRecognizers.Add(_actionTap);
        Motion.SetPress(_action, true);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };
        grid.Add(back, 0);
        grid.Add(iconBox, 1);
        grid.Add(texts, 2);
        grid.Add(_action, 3);

        // Dekorativ işıq ləkəsi — başlığın fonunda yavaş hərəkət edir (toxunuşları keçirir)
        var glow = new Ellipse
        {
            WidthRequest = 190, HeightRequest = 190,
            Fill = new RadialGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromRgba(255, 255, 255, 0.16), 0f),
                new GradientStop(Color.FromRgba(255, 255, 255, 0), 1f)
            }),
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -90, -50, 0),
            InputTransparent = true
        };
        var layered = new Grid { Children = { glow, grid } };

        Content = new Border
        {
            Style = Res<Style>("PageHeader"),
            Padding = new Thickness(12, 12, 16, 16),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(0, 0, 24, 24) },
            Content = layered
        };

        // Animasiyalar səhifə görünəndə başlayır, bağlananda dayanır (batareya / CPU)
        Loaded += (_, _) =>
        {
            Motion.Drift(glow, dx: -60, dy: 18);
            Motion.Float(iconBox, amplitude: 2.5);
        };
        Unloaded += (_, _) =>
        {
            glow.AbortAnimation("de-drift");
            iconBox.AbortAnimation("de-float");
        };
    }

    private void UpdateAction() =>
        _action.IsVisible = ActionVisible && !string.IsNullOrEmpty(ActionText) && ActionCommand != null;

    private static async void GoBack()
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { Services.CrashLog.Write(ex, "BackHeader.Back"); }
    }

    private static T Res<T>(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var v) && v is T t ? t : default!;
}
