using System.ComponentModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using DoctorEngine.App.Services;
using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Views;

public partial class ReelsPage : ContentPage
{
    private const double SwipeThreshold = 70;      // bu qədər sürüşdürəndə növbəti video
    private const int ViewAfterSeconds = 3;        // bu qədər izlənəndə baxış sayılır

    private readonly ReelsViewModel _vm;
    private ReelItem? _playing;
    private bool _animating;
    private bool _visible;
    private bool _userPaused;
    private double _panY;
    private DateTime _lastTap;
    private CancellationTokenSource? _tapCts;

    /// <summary>Rəylər pəncərəsi açılanda video dayanmasın (səhifə "görünməz" olur, amma arxada oynayır).</summary>
    public static bool KeepPlayingInBackground { get; set; }

    public ReelsPage(ReelsViewModel vm)
    {
        InitializeComponent();
        this.UseBottomNav("reels", dark: true, floating: false);   // Instagram kimi alt menyu (ikonlar)
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnVmChanged;
#if WINDOWS
        HookDesktopInput();
#endif

        Player.MediaOpened += (_, _) => MainThread.BeginInvokeOnMainThread(OnMediaOpened);
        Player.StateChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() => OnStateChanged(e.NewState));
        Player.PositionChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() => OnPosition(e.Position));
        Player.MediaFailed += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
        {
            Spinner.IsVisible = false;
            CrashLog.Write(new Exception(e.ErrorMessage), "Reels.MediaFailed " + _playing?.VideoUrl);
        });
    }

    // ================= HƏYAT DÖVRÜ =================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        KeepPlayingInBackground = false;
        if (Window != null)
        {
            Window.Stopped -= OnWindowStopped;
            Window.Resumed -= OnWindowResumed;
            Window.Stopped += OnWindowStopped;
            Window.Resumed += OnWindowResumed;
        }
        try
        {
            await _vm.LoadAsync();
            ShowCurrent();
            if (!_userPaused) Player.Play();
            _ = _vm.CheckForNewAsync();   // yeni paylaşılıbsa — yuxarıda "Yeni videolar"
        }
        catch (Exception ex) { CrashLog.Write(ex, "ReelsPage"); }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (KeepPlayingInBackground) return;
        _visible = false;
        Player.Pause();
    }

    private void OnWindowStopped(object? sender, EventArgs e) => Player.Pause();

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        if (_visible && !_userPaused && Shell.Current?.CurrentPage == this) Player.Play();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReelsViewModel.Current) && !_animating) ShowCurrent();
        else if (e.PropertyName == nameof(ReelsViewModel.IsMuted))
            MuteIcon.Source = _vm.IsMuted ? "ic_mute_w.png" : "ic_volume_w.png";
    }

    // ================= OYNATMA =================

    /// <summary>Cari videonu oynadıcıya yüklə (eyni videodursa — toxunma).</summary>
    private void ShowCurrent()
    {
        var item = _vm.Current;
        if (item == _playing) return;
        _playing = item;
        _userPaused = false;
        PausedBadge.IsVisible = false;
        ProgressFill.WidthRequest = 0;
        CaptionLabel.MaxLines = 2;

        if (item == null)
        {
            Player.Stop();
            Player.Source = null;
            return;
        }
        Spinner.IsVisible = true;
        Player.Aspect = Aspect.AspectFill;
        Player.Source = MediaSource.FromUri(item.VideoUrl);
        if (_visible) Player.Play();
    }

    private void OnMediaOpened()
    {
        Spinner.IsVisible = false;
        // Üfüqi video kəsilməsin — tam görünsün; şaquli (telefon) video ekranı doldursun
        Player.Aspect = Player.MediaWidth > Player.MediaHeight ? Aspect.AspectFit : Aspect.AspectFill;
        if (!_visible || _userPaused) Player.Pause();
    }

    private void OnStateChanged(MediaElementState state)
    {
        Spinner.IsVisible = state is MediaElementState.Opening or MediaElementState.Buffering;
    }

    private void OnPosition(TimeSpan position)
    {
        var duration = Player.Duration.TotalMilliseconds;
        var w = Stage.Width;
        if (duration > 0 && w > 0)
            ProgressFill.WidthRequest = Math.Clamp(position.TotalMilliseconds / duration, 0, 1) * w;
        if (_playing != null && position.TotalSeconds >= ViewAfterSeconds) _vm.MarkViewed(_playing);
    }

    private void TogglePause()
    {
        if (_playing == null) return;
        if (Player.CurrentState == MediaElementState.Playing)
        {
            _userPaused = true;
            Player.Pause();
            PausedBadge.IsVisible = true;
            PausedBadge.Scale = 0.6;
            _ = PausedBadge.ScaleToAsync(1, 160, Easing.CubicOut);
        }
        else
        {
            _userPaused = false;
            Player.Play();
            PausedBadge.IsVisible = false;
        }
    }

    // ================= TOXUNUŞ =================

    /// <summary>Bir toxunuş — dayandır / davam et; 300 ms ərzində ikinci toxunuş — bəyən.</summary>
    private async void OnStageTapped(object? sender, TappedEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTap).TotalMilliseconds < 300)
        {
            _lastTap = DateTime.MinValue;
            _tapCts?.Cancel();
            await DoubleTapLikeAsync(e.GetPosition(Stage));
            return;
        }
        _lastTap = now;
        _tapCts?.Cancel();
        var cts = _tapCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, cts.Token);
            TogglePause();
        }
        catch (TaskCanceledException) { }
    }

    private async Task DoubleTapLikeAsync(Point? at)
    {
        BigHeart.TranslationX = at is { } p ? p.X - Stage.Width / 2 : 0;
        BigHeart.TranslationY = at is { } q ? q.Y - Stage.Height / 2 : 0;
        BigHeart.Rotation = Random.Shared.Next(-15, 16);
        BigHeart.Scale = 0.3;
        BigHeart.Opacity = 1;
        _ = _vm.DoubleTapLikeAsync();
        await BigHeart.ScaleToAsync(1.15, 140, Easing.CubicOut);
        await BigHeart.ScaleToAsync(1.0, 90);
        await Task.Delay(350);
        await Task.WhenAll(BigHeart.FadeToAsync(0, 220), BigHeart.TranslateToAsync(BigHeart.TranslationX, BigHeart.TranslationY - 80, 220));
    }

    private async void OnLikeTapped(object? sender, TappedEventArgs e)
    {
        var willLike = _vm.Current is { Liked: false };
        await _vm.ToggleLikeCommand.ExecuteAsync(null);
        if (willLike && _vm.Current?.Liked == true)
        {
            await LikeIcon.ScaleToAsync(1.35, 110, Easing.CubicOut);
            await LikeIcon.ScaleToAsync(1, 140, Easing.SpringOut);
        }
    }

    private void OnCommentsTapped(object? sender, TappedEventArgs e)
    {
        KeepPlayingInBackground = true;
        _vm.OpenCommentsCommand.Execute(null);
    }

    private void OnShareTapped(object? sender, TappedEventArgs e) => _vm.ShareCommand.Execute(null);

    private void OnMoreTapped(object? sender, TappedEventArgs e) => _vm.MoreCommand.Execute(null);

    private void OnCaptionTapped(object? sender, TappedEventArgs e) =>
        CaptionLabel.MaxLines = CaptionLabel.MaxLines == 2 ? 12 : 2;

    // ================= SÜRÜŞDÜRMƏ (barmaq / siçan) =================

    private const double PullToRefresh = 90;   // ilk videoda bu qədər aşağı dartanda — yenilə
    private bool _refreshing;

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started: DragStart(); break;
            case GestureStatus.Running: DragMove(e.TotalY); break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled: DragEnd(); break;
        }
    }

    private void DragStart()
    {
        if (_animating || _refreshing) return;
        _panY = 0;
    }

    private void DragMove(double totalY)
    {
        if (_animating || _refreshing) return;
        _panY = totalY;
        // İlk videoda aşağı dartmaq — "yenilə" (yuxarıda dairə görünür, ox çevrilir)
        if (_panY > 0 && !_vm.CanGoUp)
        {
            Stage.TranslationY = _panY * 0.35;
            var p = Math.Clamp(_panY / PullToRefresh, 0, 1);
            PullIndicator.Opacity = p;
            PullIndicator.TranslationY = p * 24;
            PullArrow.Rotation = p >= 1 ? 180 : 0;
            return;
        }
        // Lentin sonunda — "rezin" kimi az hərəkət
        Stage.TranslationY = _panY < 0 && !_vm.CanGoDown ? _panY * 0.25 : _panY;
    }

    private void DragEnd()
    {
        if (_animating || _refreshing) return;
        var dy = _panY;
        _panY = 0;
        if (dy <= -SwipeThreshold && _vm.CanGoDown) _ = MoveAsync(1, dy);
        else if (dy >= SwipeThreshold && _vm.CanGoUp) _ = MoveAsync(-1, dy);
        else if (dy >= PullToRefresh && !_vm.CanGoUp) _ = RefreshAsync();
        else
        {
            _ = Stage.TranslateToAsync(0, 0, 180, Easing.CubicOut);
            _ = PullIndicator.FadeToAsync(0, 150);
        }
    }

    /// <summary>Lenti yenilə və ən yeni videoya qayıt (aşağı dartanda / "Yeni videolar" düyməsi / siçan təkəri).</summary>
    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            PullIndicator.Opacity = 1;
            PullIndicator.TranslationY = 24;
            PullArrow.IsVisible = false;
            PullSpinner.IsVisible = PullSpinner.IsRunning = true;
            _ = Stage.TranslateToAsync(0, 0, 200, Easing.CubicOut);

            await _vm.RefreshToTopAsync();
            ShowCurrent();
        }
        catch (Exception ex) { CrashLog.Write(ex, "Reels.Refresh"); }
        finally
        {
            PullSpinner.IsVisible = PullSpinner.IsRunning = false;
            PullArrow.IsVisible = true;
            PullArrow.Rotation = 0;
            await PullIndicator.FadeToAsync(0, 200);
            PullIndicator.TranslationY = 0;
            _refreshing = false;
        }
    }

    private void OnNewPillTapped(object? sender, TappedEventArgs e) => _ = RefreshAsync();

#if WINDOWS
    // Windows: siçan təkəri — növbəti / əvvəlki video (ilk videoda yuxarı fırlatmaq — yenilə);
    // siçanla dartmaq — barmaq kimi (WinUI-də Pan jesti siçanla işləmir).
    private DateTime _lastWheel;
    private bool _mouseDrag;
    private double _mouseStartY;

    private void HookDesktopInput()
    {
        TouchLayer.HandlerChanged += (_, _) =>
        {
            if (TouchLayer.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement el) return;
            el.PointerWheelChanged += (_, e) =>
            {
                e.Handled = true;
                if (_animating || _refreshing || (DateTime.UtcNow - _lastWheel).TotalMilliseconds < 450) return;
                _lastWheel = DateTime.UtcNow;
                var delta = e.GetCurrentPoint(el).Properties.MouseWheelDelta;
                if (delta < 0 && _vm.CanGoDown) _ = MoveAsync(1, 0);
                else if (delta > 0 && _vm.CanGoUp) _ = MoveAsync(-1, 0);
                else if (delta > 0) _ = RefreshAsync();
            };
            el.PointerPressed += (_, e) =>
            {
                if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;
                _mouseDrag = true;
                _mouseStartY = e.GetCurrentPoint(el).Position.Y;
                DragStart();
            };
            el.PointerMoved += (_, e) =>
            {
                if (!_mouseDrag) return;
                DragMove(e.GetCurrentPoint(el).Position.Y - _mouseStartY);
            };
            void Release()
            {
                if (!_mouseDrag) return;
                _mouseDrag = false;
                DragEnd();
            }
            el.PointerReleased += (_, _) => Release();
            el.PointerCaptureLost += (_, _) => Release();
            el.PointerExited += (_, _) => Release();
        };
    }
#endif

    /// <summary>Cari videonu yuxarı (delta=1) və ya aşağı (delta=-1) çıxar, yenisini əks tərəfdən gətir.</summary>
    private async Task MoveAsync(int delta, double fromY)
    {
        if (_animating) return;
        _animating = true;
        try
        {
            var h = Math.Max(Stage.Height, 400);
            Stage.TranslationY = fromY;
            await Stage.TranslateToAsync(0, -delta * h, 170, Easing.CubicIn);

            var moved = await _vm.MoveAsync(delta);
            ShowCurrent();

            Stage.TranslationY = moved ? delta * h : 0;
            await Stage.TranslateToAsync(0, 0, 230, Easing.CubicOut);
        }
        catch (Exception ex) { CrashLog.Write(ex, "Reels.Move"); Stage.TranslationY = 0; }
        finally { _animating = false; }
    }
}
