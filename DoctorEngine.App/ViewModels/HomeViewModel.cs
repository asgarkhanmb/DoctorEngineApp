using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public HomeViewModel(ContentService content) => _content = content;

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
        catch { await Alert("Zəng", $"Zəng etmək mümkün olmadı. Nömrə: {PrimaryPhone}"); }
    }

    [RelayCommand] private Task OpenWhatsApp() => OpenAsync(WhatsApp?.Link);

    [RelayCommand]
    private Task OpenMap() => OpenAsync(string.IsNullOrWhiteSpace(Map?.Address) ? null
        : "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(Map!.Address!));

    [RelayCommand]
    private Task Book(CategoryItem? category) =>
        Shell.Current.GoToAsync(category?.ReservationServiceId is int id ? $"//booking?serviceId={id}" : "//booking");

    [RelayCommand] private Task PlayVideo(string? url) => OpenAsync(url);

    [RelayCommand] private Task AllServices() => Shell.Current.GoToAsync("//services");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var i in items) target.Add(i);
    }
}

public record SlideItem(string? Title, string? Subtitle, string? BackgroundUrl, string? SideImageUrl);
public record InformItem(string? Title, string? Description, string? IconUrl);
public record TechnicianItem(string FullName, string? Position, string? Bio, string? ImageUrl);

public record VideoItem(string? Title, string? Description, string? VideoUrl);

public record CategoryItem(int Id, string Name, string? IconUrl, int? ReservationServiceId, IReadOnlyList<VideoItem> Videos)
{
    public bool HasVideo => Videos.Count > 0;
    public string? FirstVideoUrl => Videos.FirstOrDefault()?.VideoUrl;
    public string? Description => Videos.FirstOrDefault()?.Description;

    public static CategoryItem From(CategoryDto c) => new(
        c.Id, c.Name, AppConfig.Absolute(c.IconUrl), c.ReservationServiceId,
        c.Videos.Select(v => new VideoItem(v.Title, v.Description, AppConfig.Absolute(v.VideoUrl))).ToList());
}
