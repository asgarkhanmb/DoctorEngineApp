using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private IDispatcherTimer? _slideTimer;
    private bool _revealed;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        this.CenterOnWideScreens();
        this.UseHeaderStatusBar();
        BindingContext = _vm = vm;

        Motion.Pulse(LiveDot);
        Motion.Shimmer(SkeletonHero);
        foreach (var child in SkeletonCards.Children.OfType<VisualElement>()) Motion.Shimmer(child);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Motion.Enter(this);   // tab dəyişəndə məzmun yumşaq daxil olur
        _vm.RefreshLive();
        StartSlideTimer();
        try
        {
            if (_vm.Slides.Count == 0 && !_vm.IsBusy) await _vm.LoadAsync();
        }
        catch (Exception ex) { Services.CrashLog.Write(ex, "HomePage"); }

        // Bölmələr ilk dəfə (məlumat gələndən sonra) növbə ilə üzə çıxır
        if (!_revealed && _vm.HasSlides)
        {
            _revealed = true;
            Motion.RevealSections(this);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _slideTimer?.Stop();
    }

    /// <summary>Slayder özü dəyişir (4.5 san), sonuncudan sonra birinciyə qayıdır.</summary>
    private void StartSlideTimer()
    {
        _slideTimer ??= CreateSlideTimer();
        _slideTimer.Start();
    }

    private IDispatcherTimer CreateSlideTimer()
    {
        var t = Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromSeconds(4.5);
        t.Tick += (_, _) =>
        {
            var n = _vm.Slides.Count;
            if (n < 2) return;
            try { Hero.ScrollTo((Hero.Position + 1) % n, animate: true); } catch { }
        };
        return t;
    }
}
