using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

public partial class NotificationItem : ObservableObject
{
    public NotificationItem(NotificationDto dto)
    {
        Dto = dto;
        isRead = dto.IsRead;
        CreatedAt = DateTime.TryParse(dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Now;
    }

    public NotificationDto Dto { get; }
    public int Id => Dto.Id;
    public DateTime CreatedAt { get; }
    public string ActorName => Dto.ActorName;
    public string? AvatarUrl => AppConfig.Absolute(Dto.ActorAvatarUrl);
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);
    public string Initial => string.IsNullOrEmpty(ActorName) ? "?" : ActorName[..1].ToUpperInvariant();

    /// <summary>"videonuzu bəyəndi" və s. — seçilmiş dildə.</summary>
    public string Action => Dto.Type switch
    {
        "reel_like" => Lang.T("notif_like"),
        "reel_comment" => Lang.T("notif_comment"),
        "comment_reply" => Lang.T("notif_reply"),
        "comment_like" => Lang.T("notif_comment_like"),
        _ => ""
    };

    public string? Snippet => string.IsNullOrWhiteSpace(Dto.Snippet) ? null : Dto.Snippet;
    public bool HasSnippet => Snippet != null;
    /// <summary>": mətn" — bəyənmədə boş (artıq iki nöqtə qalmasın).</summary>
    public string SnippetText => Snippet == null ? "" : ": " + Snippet;
    public bool IsLike => Dto.Type is "reel_like" or "comment_like";
    public string TypeIcon => IsLike ? "ic_heart_r.png" : "ic_comment_w.png";
    /// <summary>Telefonun bildiriş panelindəki mətn.</summary>
    public string PanelText => $"{ActorName} {Action}{SnippetText}";
    public string TimeAgo => ReelItem.Ago(CreatedAt);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsUnread))] private bool isRead;
    public bool IsUnread => !IsRead;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Action));
        OnPropertyChanged(nameof(TimeAgo));
    }
}

/// <summary>
/// Bildirişlər (Instagram-dakı "ürək"): siyahı, oxundu / oxunmadı, silmək. Alt menyudakı nişan üçün
/// oxunmamışların sayı hər 30 saniyədən bir (və tətbiq önə gələndə) yenilənir.
/// </summary>
public partial class NotificationsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private readonly ReelsViewModel _reels;
    private IDispatcherTimer? _timer;

    public ObservableCollection<NotificationItem> Items { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasUnread), nameof(UnreadText))] private int unreadCount;
    [ObservableProperty] private bool hasMore;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isRefreshing;

    public bool HasUnread => UnreadCount > 0;
    public string UnreadText => UnreadCount > 99 ? "99+" : UnreadCount.ToString(CultureInfo.InvariantCulture);
    public bool IsLoggedIn => _auth.IsLoggedIn;

    public NotificationsViewModel(ApiClient api, AuthService auth, ReelsViewModel reels)
    {
        _api = api;
        _auth = auth;
        _reels = reels;
        _auth.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(IsLoggedIn));
            Items.SyncWith([]);
            IsEmpty = false;
            UnreadCount = 0;
            _ = RefreshCountAsync();
        });
    }

    protected override void OnLanguageChanged()
    {
        foreach (var i in Items) i.Refresh();
    }

    /// <summary>Nişan üçün fon yeniləməsi (App başlayanda bir dəfə çağırılır).</summary>
    public void StartPolling()
    {
        if (_timer != null) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick += async (_, _) => await RefreshCountAsync();
        _timer.Start();
        _ = RefreshCountAsync();
    }

    public async Task RefreshCountAsync()
    {
        if (!_auth.IsLoggedIn) { UnreadCount = 0; return; }
        try
        {
            var previous = UnreadCount;
            UnreadCount = (await _api.GetAsync<UnreadCountDto>("api/v1/notifications/unread-count")).Count;
            // Arxa plandadırsa — yeniləri telefonun bildiriş panelinə çıxar; önündədirsə — yalnız "görüldü" say
            // (sonra arxa plana keçəndə köhnələr panelə tökülməsin)
            if (UnreadCount > 0 && (!App.IsInForeground || UnreadCount != previous))
                await ShowNewInPanelAsync(show: !App.IsInForeground);
        }
        catch { /* oflayn — nişan olduğu kimi qalır */ }
    }

    private readonly SemaphoreSlim _panelLock = new(1, 1);

    /// <summary>
    /// Hələ göstərilməmiş oxunmamış bildirişləri panelə çıxarır (hər biri bir dəfə — son göstərilən Id yadda saxlanılır).
    /// Android WorkManager də (tətbiq bağlı olanda) bunu çağırır.
    /// </summary>
    public async Task ShowNewInPanelAsync(bool show = true)
    {
        if (!_auth.IsLoggedIn || _auth.User == null) return;
        if (!await _panelLock.WaitAsync(0)) return;
        try
        {
            var key = "notif_last_shown_" + _auth.User.UserName;
            var lastShown = Preferences.Default.Get(key, -1);
            var page = await _api.GetAsync<NotificationPageDto>("api/v1/notifications?take=10");
            var maxId = page.Items.Count == 0 ? 0 : page.Items.Max(n => n.Id);
            if (lastShown < 0)
            {
                // İlk dəfə — köhnə bildirişlərlə paneli doldurmayaq
                Preferences.Default.Set(key, maxId);
                return;
            }
            if (show)
            {
                foreach (var n in page.Items.Where(n => !n.IsRead && n.Id > lastShown).OrderBy(n => n.Id))
                {
                    var item = new NotificationItem(n);
                    LocalNotifier.Show(n.Id, "DoctorEngine", item.PanelText, n.ReelId, !item.IsLike || n.Type == "comment_like");
                }
            }
            if (maxId > lastShown) Preferences.Default.Set(key, maxId);
        }
        catch { /* oflayn */ }
        finally { _panelLock.Release(); }
    }

    public async Task LoadAsync()
    {
        if (!_auth.IsLoggedIn) { Items.SyncWith([]); IsEmpty = false; return; }
        await RunAsync(async () =>
        {
            var page = await _api.GetAsync<NotificationPageDto>("api/v1/notifications");
            Items.SyncWith(page.Items.Select(d => new NotificationItem(d)));
            HasMore = page.HasMore;
            UnreadCount = page.Unread;
            IsEmpty = Items.Count == 0;
        });
    }

    [RelayCommand]
    private async Task Refresh()
    {
        IsRefreshing = true;
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (!HasMore || Items.Count == 0 || IsBusy) return;
        try
        {
            var page = await _api.GetAsync<NotificationPageDto>($"api/v1/notifications?before={Items[^1].Id}");
            foreach (var d in page.Items) Items.Add(new NotificationItem(d));
            HasMore = page.HasMore;
        }
        catch (ApiException ex) { ErrorMessage = ex.Message; }
    }

    /// <summary>Bildirişə toxunanda: oxundu et və videonu (rəydirsə — rəyləri) aç.</summary>
    [RelayCommand]
    private async Task Open(NotificationItem? item)
    {
        if (item == null) return;
        if (!item.IsRead) await SetReadAsync(item, true);
        if (item.Dto.ReelId is int reelId)
        {
            await Shell.Current.GoToAsync("//reels");
            await _reels.ShowReelAsync(reelId, openComments: item.Dto.Type != "reel_like");
        }
    }

    [RelayCommand]
    private Task ToggleRead(NotificationItem? item) => item == null ? Task.CompletedTask : SetReadAsync(item, !item.IsRead);

    private async Task SetReadAsync(NotificationItem item, bool read)
    {
        var was = item.IsRead;
        item.IsRead = read;
        UnreadCount = Math.Max(0, UnreadCount + (read ? -1 : 1));
        try { await _api.PostAsync($"api/v1/notifications/{item.Id}/read?read={(read ? "true" : "false")}", null); }
        catch (ApiException ex)
        {
            item.IsRead = was;
            UnreadCount = Math.Max(0, UnreadCount + (read ? 1 : -1));
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Delete(NotificationItem? item)
    {
        if (item == null) return;
        try
        {
            await _api.DeleteAsync($"api/v1/notifications/{item.Id}");
            Items.Remove(item);
            if (!item.IsRead) UnreadCount = Math.Max(0, UnreadCount - 1);
            IsEmpty = Items.Count == 0;
        }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
    }

    [RelayCommand]
    private async Task ReadAll()
    {
        if (!HasUnread) return;
        try
        {
            await _api.PostAsync("api/v1/notifications/read-all", null);
            foreach (var i in Items) i.IsRead = true;
            UnreadCount = 0;
        }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteAll()
    {
        if (Items.Count == 0) return;
        if (!await Confirm(Lang.T("notif_delete_all"), Lang.T("notif_delete_all_confirm"), Lang.T("notif_delete_all"))) return;
        try
        {
            await _api.DeleteAsync("api/v1/notifications");
            Items.SyncWith([]);
            UnreadCount = 0;
            IsEmpty = true;
        }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); }
    }

    [RelayCommand]
    private Task Login() => Shell.Current.GoToAsync("//profile");
}
