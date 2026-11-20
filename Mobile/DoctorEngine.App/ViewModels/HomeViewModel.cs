using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>Ana səhifə: slayder, üstünlüklər, xidmətlər, ustalar, əlaqə — saytın ana səhifəsi ilə eyni məzmun.</summary>
public partial class HomeViewModel : BaseViewModel
{
    private readonly ContentService _content;

    [ObservableProperty] private string siteName = "DoctorEngine.az";
    [ObservableProperty] private string? logoUrl;
    [ObservableProperty] private bool isOffline;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private ContactDto? contact;
    [ObservableProperty] private WhatsAppDto? whatsApp;
    [ObservableProperty] private MapDto? map;
    [ObservableProperty] private string? primaryPhone;

    public ObservableCollection<SlideItem> Slides { get; } = new();
    public ObservableCollection<InformItem> Informs { get; } = new();
    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<TechnicianItem> Technicians { get; } = new();

    private readonly AuthService _auth;

    // ---- Başlıq: salamlama + iş vəziyyəti ("canlı" status) ----
    [ObservableProperty] private string greeting = "";
    [ObservableProperty] private string greetingName = "";
    [ObservableProperty] private bool isOpenNow;
    [ObservableProperty] private string openStatus = "";
    [ObservableProperty] private string todayHours = "";

    public bool HasSlides => Slides.Count > 0;
    public bool ShowSkeleton => IsBusy && Slides.Count == 0;

    public HomeViewModel(ContentService content, AuthService auth)
    {
        _content = content;
        _auth = auth;
        _auth.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateGreeting);
        Slides.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasSlides)); OnPropertyChanged(nameof(ShowSkeleton)); };
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(ShowSkeleton)); };
        UpdateGreeting();
    }

    /// <summary>Səhifə hər göstəriləndə: salamlama və "indi açıqdır" vəziyyəti yenilənir.</summary>
    public void RefreshLive()
    {
        UpdateGreeting();
        UpdateOpenStatus();
    }

    private void UpdateGreeting()
    {
        var h = DateTime.Now.Hour;
        Greeting = h switch
        {
            >= 5 and < 12 => Lang.T("greet_morning"),
            >= 12 and < 18 => Lang.T("greet_afternoon"),
            >= 18 and < 23 => Lang.T("greet_evening"),
            _ => Lang.T("greet_night")
        };
        var first = _auth.User?.FullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        GreetingName = string.IsNullOrWhiteSpace(first) ? Lang.T("greet_welcome_excl") : Lang.T("greet_welcome_name", first);
    }

    /// <summary>Saytdakı iş saatlarından ("10:00 - 20:00") hazırda açıq olub-olmadığını hesablayır.</summary>
    private void UpdateOpenStatus()
    {
        var now = DateTime.Now;
        var weekend = now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var range = weekend ? Contact?.Weekend : Contact?.Weekdays;
        TodayHours = string.IsNullOrWhiteSpace(range) ? "" : range.Replace(" ", "");
        var m = System.Text.RegularExpressions.Regex.Match(range ?? "", @"(\d{1,2}):(\d{2})\s*[-–]\s*(\d{1,2}):(\d{2})");
        if (!m.Success) { IsOpenNow = false; OpenStatus = ""; return; }
        var open = new TimeSpan(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0);
        var close = new TimeSpan(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), 0);
        var t = now.TimeOfDay;
        IsOpenNow = t >= open && t < close;
        OpenStatus = IsOpenNow
            ? Lang.T("open_now", close.ToString(@"hh\:mm"))
            : t < open ? Lang.T("closed_opens_at", open.ToString(@"hh\:mm")) : Lang.T("closed_tomorrow");
    }

    protected override void OnLanguageChanged()
    {
        UpdateGreeting();
        UpdateOpenStatus();
        // Kartlardakı standart mətnlər (təsvir yoxdursa) yenidən çəkilsin
        Categories.Rebind();
    }

    [RelayCommand] private Task OpenChat() => Shell.Current.GoToAsync("//chat");
    [RelayCommand] private Task OpenAi() => Shell.Current.GoToAsync("ai");

    [RelayCommand]
    public async Task LoadAsync(bool force = false)
    {
        await RunAsync(async () =>
        {
            var home = await _content.GetHomeAsync(force);
            if (home == null) return;
            IsOffline = !_content.IsFresh;

            SiteName = home.Site.Name;
            LogoUrl = AppConfig.Absolute(home.Site.LogoUrl);
            Contact = home.Contact;
            WhatsApp = home.WhatsApp;
            Map = home.Map;
            PrimaryPhone = home.Phones.FirstOrDefault()?.PhoneNumber ?? home.Contact?.Phone;
            UpdateOpenStatus();

            Replace(Slides, home.Slides.Select(s => new SlideItem(s.Title, s.Subtitle, AppConfig.Absolute(s.BackgroundUrl), AppConfig.Absolute(s.SideImageUrl))));
            Replace(Informs, home.Informs.Select(i => new InformItem(i.Title, i.Description, AppConfig.Absolute(i.IconUrl))));
            // Saytdakı kimi: ana səhifədə ən son yaradılan 3 xidmət
            Replace(Categories, home.Categories.OrderByDescending(c => c.Id).Take(3).Select(CategoryItem.From));
            Replace(Technicians, home.Technicians.Select(t => new TechnicianItem(t.FullName, t.Position, t.Bio, AppConfig.Absolute(t.ImageUrl))));
        });
        IsRefreshing = false;
    }

    [RelayCommand] private Task Refresh() => LoadAsync(true);

    [RelayCommand]
    private async Task Call()
    {
        if (string.IsNullOrWhiteSpace(PrimaryPhone)) return;
        try { PhoneDialer.Default.Open(PrimaryPhone.Replace(" ", "")); }
        catch { await Alert(Lang.T("call_title"), Lang.T("call_failed", PrimaryPhone)); }
    }

    [RelayCommand] private Task OpenWhatsApp() => OpenAsync(WhatsApp?.Link);

    [RelayCommand]
    private Task OpenMap() => OpenAsync(string.IsNullOrWhiteSpace(Map?.Address) ? null
        : "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(Map!.Address!));

    [RelayCommand]
    private Task Book(CategoryItem? category) =>
        Shell.Current.GoToAsync(category?.ReservationServiceId is int id ? $"booking?serviceId={id}" : "booking");

    [RelayCommand] private Task PlayVideo(string? url) => OpenAsync(url);

    [RelayCommand] private Task AllServices() => Shell.Current.GoToAsync("//services");

    /// <summary>Clear() yox — Windows-da yeniləmə zamanı çökmənin qarşısını alır (bax CollectionSync).</summary>
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items) => target.SyncWith(items);
}

public record SlideItem(string? Title, string? Subtitle, string? BackgroundUrl, string? SideImageUrl);
public record InformItem(string? Title, string? Description, string? IconUrl);
public record TechnicianItem(string FullName, string? Position, string? Bio, string? ImageUrl);

public record VideoItem(string? Title, string? Description, string? VideoUrl)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Lang.T("common_watch_video") : Title;
}

public record CategoryItem(int Id, string Name, string? IconUrl, int? ReservationServiceId, IReadOnlyList<VideoItem> Videos)
{
    public bool HasVideo => Videos.Count > 0;
    public string? FirstVideoUrl => Videos.FirstOrDefault()?.VideoUrl;
    public string? Description => Videos.FirstOrDefault()?.Description;
    /// <summary>Təsvir yoxdursa — seçilmiş dildə standart mətn (ana səhifə / xidmətlər).</summary>
    public string ShortDescription => string.IsNullOrWhiteSpace(Description) ? Lang.T("cat_desc_short") : Description;
    public string LongDescription => string.IsNullOrWhiteSpace(Description) ? Lang.T("cat_desc_long") : Description;

    public static CategoryItem From(CategoryDto c) => new(
        c.Id, c.Name, AppConfig.Absolute(c.IconUrl), c.ReservationServiceId,
        c.Videos.Select(v => new VideoItem(v.Title, v.Description, AppConfig.Absolute(v.VideoUrl))).ToList());
}
