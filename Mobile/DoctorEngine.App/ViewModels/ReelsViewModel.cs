using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>Lentdəki bir video — bəyənmə / rəy sayı canlı yenilənir.</summary>
public partial class ReelItem : ObservableObject
{
    public ReelItem(ReelDto dto) => Apply(dto);

    public int Id { get; private set; }
    public string VideoUrl { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string? AuthorAvatarUrl { get; private set; }
    public bool HasAvatar => !string.IsNullOrEmpty(AuthorAvatarUrl);
    public string Initial => string.IsNullOrEmpty(Author) ? "D" : Author[..1].ToUpperInvariant();
    public DateTime CreatedAt { get; private set; }
    public bool CanDelete { get; private set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCaption))] private string? caption;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LikesText))] private int likes;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LikeIcon))] private bool liked;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CommentsText))] private int comments;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ViewsText))] private int views;
    [ObservableProperty] private bool captionExpanded;

    public bool HasCaption => !string.IsNullOrWhiteSpace(Caption);
    public string LikesText => Compact(Likes);
    public string CommentsText => Compact(Comments);
    public string ViewsText => Lang.T("reels_views", Compact(Views));
    public string LikeIcon => Liked ? "ic_heart_r.png" : "ic_heart_w.png";
    public string TimeAgo => Ago(CreatedAt);

    /// <summary>Baxış bir dəfə sayılsın.</summary>
    public bool ViewCounted { get; set; }

    public void Apply(ReelDto dto)
    {
        Id = dto.Id;
        VideoUrl = AppConfig.Absolute(dto.VideoUrl) ?? "";
        Author = dto.Author;
        AuthorAvatarUrl = AppConfig.Absolute(dto.AuthorAvatarUrl);
        CreatedAt = DateTime.TryParse(dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Now;
        CanDelete = dto.CanDelete;
        Caption = dto.Caption;
        Likes = dto.Likes;
        Liked = dto.Liked;
        Comments = dto.Comments;
        Views = dto.Views;
        OnPropertyChanged(string.Empty);
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(TimeAgo));
        OnPropertyChanged(nameof(ViewsText));
    }

    public static string Compact(int n) => n switch
    {
        >= 1_000_000 => (n / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 10_000 => (n / 1000) + "K",
        >= 1000 => (n / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => n.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>Server vaxtı Bakı vaxtıdır (UTC+4, yay vaxtı yoxdur).</summary>
    public static string Ago(DateTime bakuTime)
    {
        var span = DateTime.UtcNow - bakuTime.AddHours(-4);
        if (span.TotalMinutes < 1) return Lang.T("reels_now");
        if (span.TotalHours < 1) return Lang.T("reels_min", (int)span.TotalMinutes);
        if (span.TotalDays < 1) return Lang.T("reels_hour", (int)span.TotalHours);
        if (span.TotalDays < 7) return Lang.T("reels_day", (int)span.TotalDays);
        if (span.TotalDays < 60) return Lang.T("reels_week", (int)(span.TotalDays / 7));
        return bakuTime.ToString("dd.MM.yyyy");
    }
}

/// <summary>
/// "Videolar" bölməsi — Instagram Reels kimi şaquli lent. Ekranda bir video olur; yuxarı / aşağı sürüşdürəndə
/// növbəti / əvvəlki açılır (oynatma, animasiya — ReelsPage). Sona yaxınlaşanda növbəti səhifə yüklənir.
/// </summary>
public partial class ReelsViewModel : BaseViewModel
{
    private readonly ReelsService _reels;
    private readonly AuthService _auth;
    private bool _loaded;
    private bool _loadingMore;

    public ObservableCollection<ReelItem> Items { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCurrent), nameof(CanGoUp), nameof(CanGoDown))] private ReelItem? current;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanGoDown))] private bool hasMore;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isMuted;
    /// <summary>Serverdə lentdəkindən yeni video var — yuxarıda "Yeni videolar" düyməsi.</summary>
    [ObservableProperty] private bool hasNewVideos;

    public int Index { get; private set; } = -1;
    public bool HasCurrent => Current != null;
    public bool CanGoUp => Index > 0;
    public bool CanGoDown => Index < Items.Count - 1 || HasMore;
    public bool IsAdmin => _auth.User?.IsAdmin == true;
    public bool IsLoggedIn => _auth.IsLoggedIn;

    public ReelsViewModel(ReelsService reels, AuthService auth)
    {
        _reels = reels;
        _auth = auth;
        _auth.Changed += async (_, _) =>
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsLoggedIn));
            // Bəyənmələr / silmə icazəsi istifadəçiyə görədir — lent yenidən yüklənir
            if (_loaded) await MainThread.InvokeOnMainThreadAsync(() => LoadAsync(force: true));
        };
    }

    protected override void OnLanguageChanged()
    {
        foreach (var i in Items) i.Refresh();
    }

    public async Task LoadAsync(bool force = false)
    {
        if (_loaded && !force)
        {
            foreach (var i in Items) i.Refresh();
            return;
        }
        await RunAsync(async () =>
        {
            var keepId = Current?.Id;
            var page = await _reels.FeedAsync();
            _loaded = true;
            Items.Clear();
            foreach (var r in page.Items) Items.Add(new ReelItem(r));
            HasMore = page.HasMore;
            IsEmpty = Items.Count == 0;
            var idx = keepId == null ? 0 : Math.Max(0, Items.ToList().FindIndex(i => i.Id == keepId));
            SetIndex(Items.Count == 0 ? -1 : idx);
        });
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(force: true);

    /// <summary>Lenti yenilə və ən yeni videoya keç (aşağı dartıb yeniləmə, "Yeni videolar").</summary>
    public async Task RefreshToTopAsync()
    {
        try
        {
            var page = await _reels.FeedAsync();
            _loaded = true;
            Items.SyncWith(page.Items.Select(r => Items.FirstOrDefault(i => i.Id == r.Id) is { } old ? Updated(old, r) : new ReelItem(r)));
            HasMore = page.HasMore;
            IsEmpty = Items.Count == 0;
            HasNewVideos = false;
            ErrorMessage = null;
            SetIndex(Items.Count == 0 ? -1 : 0);
        }
        catch (ApiException ex) { ErrorMessage = ex.Message; }

        static ReelItem Updated(ReelItem item, ReelDto dto)
        {
            item.Apply(dto);   // say / bəyənmə yenilənir, oynayan video yenidən başlamır
            return item;
        }
    }

    /// <summary>Bölməyə qayıdanda: serverdə daha yeni video varsa — "Yeni videolar" düyməsi.</summary>
    public async Task CheckForNewAsync()
    {
        if (!_loaded) return;
        try
        {
            var page = await _reels.FeedAsync(take: 1);
            var newest = page.Items.FirstOrDefault()?.Id ?? 0;
            var mine = Items.Count == 0 ? 0 : Items.Max(i => i.Id);
            HasNewVideos = newest > mine;
        }
        catch { /* oflayn */ }
    }

    /// <summary>Növbəti / əvvəlki videoya keç. Uğursuzdursa false (lentin sonu / əvvəli).</summary>
    public async Task<bool> MoveAsync(int delta)
    {
        var target = Index + delta;
        if (target < 0) return false;
        if (target >= Items.Count)
        {
            await LoadMoreAsync();
            if (target >= Items.Count) return false;
        }
        SetIndex(target);
        if (Index >= Items.Count - 3) _ = LoadMoreAsync();
        return true;
    }

    private void SetIndex(int index)
    {
        Index = index;
        Current = index >= 0 && index < Items.Count ? Items[index] : null;
        OnPropertyChanged(nameof(CanGoUp));
        OnPropertyChanged(nameof(CanGoDown));
    }

    private async Task LoadMoreAsync()
    {
        if (_loadingMore || !HasMore || Items.Count == 0) return;
        _loadingMore = true;
        try
        {
            var page = await _reels.FeedAsync(Items[^1].Id);
            foreach (var r in page.Items)
                if (Items.All(i => i.Id != r.Id)) Items.Add(new ReelItem(r));
            HasMore = page.HasMore;
            OnPropertyChanged(nameof(CanGoDown));
        }
        catch (ApiException) { /* növbəti sürüşdürmədə yenidən cəhd */ }
        finally { _loadingMore = false; }
    }

    /// <summary>Video bir neçə saniyə izlənəndə — baxış sayı.</summary>
    public void MarkViewed(ReelItem item)
    {
        if (item.ViewCounted) return;
        item.ViewCounted = true;
        item.Views++;
        _ = _reels.ViewAsync(item.Id);
    }

    // ================= BƏYƏN =================

    [RelayCommand]
    private Task ToggleLike(ReelItem? item) => SetLikeAsync(item ?? Current, null);

    /// <summary>İki dəfə toxunma — yalnız bəyənir (geri almır), Instagram kimi.</summary>
    public Task DoubleTapLikeAsync() => SetLikeAsync(Current, true);

    private async Task SetLikeAsync(ReelItem? item, bool? want)
    {
        if (item == null) return;
        if (!_auth.IsLoggedIn) { await AskLoginAsync(); return; }

        var like = want ?? !item.Liked;
        if (item.Liked == like) return;
        // Dərhal göstər, server cavabı ilə düzəlt
        item.Liked = like;
        item.Likes = Math.Max(0, item.Likes + (like ? 1 : -1));
        try
        {
            var res = await _reels.LikeAsync(item.Id, like);
            item.Liked = res.Liked;
            item.Likes = res.Likes;
        }
        catch (ApiException ex)
        {
            item.Liked = !like;
            item.Likes = Math.Max(0, item.Likes + (like ? -1 : 1));
            await Alert(Lang.T("common_error"), ex.Message);
        }
    }

    // ================= RƏYLƏR / PAYLAŞ / ADMIN =================

    [RelayCommand]
    private async Task OpenComments(ReelItem? item)
    {
        item ??= Current;
        if (item == null) return;
        await Shell.Current.GoToAsync("reel-comments", new Dictionary<string, object> { ["reel"] = item });
    }

    [RelayCommand]
    private async Task Share(ReelItem? item)
    {
        item ??= Current;
        if (item == null) return;
        try
        {
            await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new Microsoft.Maui.ApplicationModel.DataTransfer.ShareTextRequest
            {
                Title = Lang.T("reels_share_text"),
                Text = string.IsNullOrWhiteSpace(item.Caption) ? Lang.T("reels_share_text") : item.Caption,
                Uri = item.VideoUrl
            });
        }
        catch (Exception ex) { CrashLog.Write(ex, "Reels.Share"); }
    }

    [RelayCommand]
    private async Task More(ReelItem? item)
    {
        item ??= Current;
        if (item == null || !IsAdmin) return;
        string edit = Lang.T("reels_edit_caption"), delete = Lang.T("reels_delete");
        var choice = await Shell.Current.DisplayActionSheetAsync(Lang.T("reels_more"), Lang.T("common_cancel"), delete, edit);
        if (choice == edit)
        {
            var text = await Shell.Current.DisplayPromptAsync(Lang.T("reels_edit_caption"), Lang.T("reels_caption"),
                Lang.T("common_save"), Lang.T("common_cancel"), Lang.T("reels_caption_placeholder"), 2200, Keyboard.Text, item.Caption ?? "");
            if (text == null) return;
            try { item.Apply(await _reels.UpdateCaptionAsync(item.Id, text)); }
            catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
        }
        else if (choice == delete)
        {
            if (!await Confirm(Lang.T("reels_delete"), Lang.T("reels_delete_confirm"), Lang.T("reels_delete"))) return;
            try
            {
                await _reels.DeleteAsync(item.Id);
                Removed(item);
            }
            catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
        }
    }

    private void Removed(ReelItem item)
    {
        var idx = Items.IndexOf(item);
        if (idx < 0) return;
        Items.RemoveAt(idx);
        IsEmpty = Items.Count == 0;
        SetIndex(Items.Count == 0 ? -1 : Math.Min(idx, Items.Count - 1));
    }

    /// <summary>Bildirişdən: həmin videonu aç (lentdə yoxdursa — yüklə və əvvələ qoy), istəyə görə rəyləri də aç.</summary>
    public async Task ShowReelAsync(int id, bool openComments)
    {
        if (!_loaded) await LoadAsync();
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item == null)
        {
            try
            {
                item = new ReelItem(await _reels.GetAsync(id));
                Items.Insert(0, item);
                IsEmpty = false;
            }
            catch (ApiException ex)
            {
                await Alert(Lang.T("common_error"), ex.Message);   // video silinib
                return;
            }
        }
        SetIndex(Items.IndexOf(item));
        if (openComments)
        {
            Views.ReelsPage.KeepPlayingInBackground = true;
            await OpenComments(item);
        }
    }

    [RelayCommand]
    private Task NewReel() => Shell.Current.GoToAsync("reel-new");

    /// <summary>Yeni video paylaşılanda (ReelNewViewModel) — lentin əvvəlinə əlavə olunur və açılır.</summary>
    public void Published(ReelDto dto)
    {
        var item = new ReelItem(dto);
        Items.Insert(0, item);
        IsEmpty = false;
        SetIndex(0);
    }

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    public async Task AskLoginAsync()
    {
        if (await Confirm(Lang.T("reels_login_title"), Lang.T("reels_login_like"), Lang.T("reels_login_btn")))
            await Shell.Current.GoToAsync("//profile");
    }
}
