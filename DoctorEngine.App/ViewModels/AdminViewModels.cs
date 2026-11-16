using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

// =====================================================================
// Mobil admin panel. Server tərəfi: api/v1/admin (yalnız Admin rolu).
// =====================================================================

/// <summary>Admin panelin ana ekranı: statistika, yaxın rezervasiyalar, bölmələr.</summary>
public partial class AdminViewModel : BaseViewModel
{
    private readonly AdminClient _admin;

    [ObservableProperty] private AdminDashboardDto? stats;
    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<AdminReservationDto> Upcoming { get; } = new();
    public ObservableCollection<AdminSectionGroup> Groups { get; } = new();

    public bool NoUpcoming => Stats != null && Upcoming.Count == 0;

    public AdminViewModel(AdminClient admin)
    {
        _admin = admin;
        Upcoming.CollectionChanged += (_, _) => OnPropertyChanged(nameof(NoUpcoming));
    }

    partial void OnStatsChanged(AdminDashboardDto? value) => OnPropertyChanged(nameof(NoUpcoming));

    protected override void OnLanguageChanged() => Upcoming.Rebind();   // status mətni konverterdən gəlir

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var dashboard = _admin.DashboardAsync();
            var sections = _admin.SectionsAsync();
            Stats = await dashboard;
            Upcoming.SyncWith(Stats.Upcoming);
            Groups.SyncWith((await sections).GroupBy(s => s.Group).Select(g => new AdminSectionGroup(g.Key, g.ToList())));
        });
        IsRefreshing = false;
    }

    [RelayCommand] private Task OpenReservations(string? status) =>
        Shell.Current.GoToAsync("admin-reservations", new Dictionary<string, object> { ["status"] = status ?? "" });
    [RelayCommand] private Task OpenMessages() => Shell.Current.GoToAsync("admin-messages");
    [RelayCommand] private Task OpenUsers() => Shell.Current.GoToAsync("admin-users");

    [RelayCommand]
    private Task OpenSection(AdminSectionDto section) =>
        section.Kind == "single"
            ? Shell.Current.GoToAsync("admin-form", new Dictionary<string, object> { ["section"] = section })
            : Shell.Current.GoToAsync("admin-section", new Dictionary<string, object> { ["section"] = section });
}

public record AdminSectionGroup(string Name, IReadOnlyList<AdminSectionDto> Sections);

/// <summary>Filtr düyməsi (Hamısı / Gözləyir / Təsdiqləndi / Rədd edildi).</summary>
public partial class FilterOption : ObservableObject
{
    public FilterOption(string key, string labelKey) { Key = key; LabelKey = labelKey; }
    public string Key { get; }
    public string LabelKey { get; }
    public string Label => Lang.T(LabelKey);
    [ObservableProperty] private bool isSelected;
    public void RefreshText() => OnPropertyChanged(nameof(Label));
}

public partial class AdminReservationsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly AdminClient _admin;

    [ObservableProperty] private bool isRefreshing;
    public ObservableCollection<AdminReservationDto> Items { get; } = new();
    public bool IsEmpty => !IsBusy && Items.Count == 0;

    public IReadOnlyList<FilterOption> Filters { get; } = new[]
    {
        new FilterOption("", "adm_all"),
        new FilterOption("Pending", "status_pending"),
        new FilterOption("Confirmed", "status_confirmed"),
        new FilterOption("Rejected", "status_rejected"),
    };

    private string _status = "";

    public AdminReservationsViewModel(AdminClient admin)
    {
        _admin = admin;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(IsEmpty)); };
        SelectFilterVisual();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("status", out var s)) _status = s as string ?? "";
        SelectFilterVisual();
    }

    protected override void OnLanguageChanged()
    {
        foreach (var f in Filters) f.RefreshText();
        Items.Rebind();
    }

    private void SelectFilterVisual()
    {
        foreach (var f in Filters) f.IsSelected = f.Key == _status;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunAsync(async () => Items.SyncWith(await _admin.ReservationsAsync(_status)));
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task SelectFilter(FilterOption option)
    {
        _status = option.Key;
        SelectFilterVisual();
        return LoadAsync();
    }

    [RelayCommand]
    private async Task Approve(AdminReservationDto r)
    {
        if (!await Confirm(Lang.T("adm_res_confirm_title"), $"{r.FullName} · {r.ServiceName}\n{r.DateTime:dd.MM.yyyy HH:mm}", Lang.T("adm_res_confirm"))) return;
        await RunAsync(async () => { await _admin.ConfirmAsync(r.Id); Items.SyncWith(await _admin.ReservationsAsync(_status)); }, showAlert: true);
    }

    [RelayCommand]
    private async Task Reject(AdminReservationDto r)
    {
        var reason = await Shell.Current.DisplayPromptAsync(Lang.T("adm_res_reject_title"), Lang.T("adm_res_reject_reason"),
            Lang.T("adm_res_reject"), Lang.T("common_cancel"), maxLength: 500);
        if (reason == null) return;
        if (string.IsNullOrWhiteSpace(reason)) { await Alert(Lang.T("common_error"), Lang.T("adm_res_reject_reason")); return; }
        await RunAsync(async () => { await _admin.RejectAsync(r.Id, reason.Trim()); Items.SyncWith(await _admin.ReservationsAsync(_status)); }, showAlert: true);
    }

    [RelayCommand]
    private async Task Delete(AdminReservationDto r)
    {
        if (!await Confirm(Lang.T("adm_delete_title"), Lang.T("adm_res_delete_text", r.FullName), Lang.T("chat_delete_btn"))) return;
        await RunAsync(async () => { await _admin.DeleteReservationAsync(r.Id); Items.SyncWith(Items.Where(i => i.Id != r.Id)); }, showAlert: true);
    }

    [RelayCommand]
    private async Task Call(AdminReservationDto r)
    {
        try { PhoneDialer.Default.Open(r.Phone.Replace(" ", "")); }
        catch { await Alert(Lang.T("call_title"), Lang.T("call_failed", r.Phone)); }
    }

    [RelayCommand]
    private Task WhatsApp(AdminReservationDto r)
    {
        var digits = new string((r.WhatsApp ?? r.Phone).Where(char.IsDigit).ToArray());
        return OpenAsync(string.IsNullOrEmpty(digits) ? null : "https://wa.me/" + digits);
    }
}

public partial class AdminMessagesViewModel : BaseViewModel
{
    private readonly AdminClient _admin;

    [ObservableProperty] private bool isRefreshing;
    public ObservableCollection<AdminMessageDto> Items { get; } = new();
    public bool IsEmpty => !IsBusy && Items.Count == 0;

    public AdminMessagesViewModel(AdminClient admin)
    {
        _admin = admin;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(IsEmpty)); };
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunAsync(async () => Items.SyncWith(await _admin.MessagesAsync()));
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task Open(AdminMessageDto m)
    {
        var reply = await Shell.Current.DisplayAlertAsync(m.Subject,
            $"{m.FullName} · {m.Email}\n{m.CreatedDate:dd.MM.yyyy HH:mm}\n\n{m.Message}",
            Lang.T("adm_msg_reply"), Lang.T("common_close"));
        if (reply) await OpenAsync($"mailto:{m.Email}?subject={Uri.EscapeDataString("Re: " + m.Subject)}");
    }

    [RelayCommand]
    private async Task Delete(AdminMessageDto m)
    {
        if (!await Confirm(Lang.T("adm_delete_title"), Lang.T("adm_msg_delete_text", m.FullName), Lang.T("chat_delete_btn"))) return;
        await RunAsync(async () => { await _admin.DeleteMessageAsync(m.Id); Items.SyncWith(Items.Where(i => i.Id != m.Id)); }, showAlert: true);
    }
}

public partial class AdminUsersViewModel : BaseViewModel
{
    private readonly AdminClient _admin;
    private List<string>? _roles;

    [ObservableProperty] private string query = "";
    [ObservableProperty] private bool isRefreshing;
    public ObservableCollection<AdminUserItem> Items { get; } = new();

    public AdminUsersViewModel(AdminClient admin) => _admin = admin;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var users = await _admin.UsersAsync(Query);
            Items.SyncWith(users.Select(u => new AdminUserItem(u)));
        });
        IsRefreshing = false;
    }

    /// <summary>Rol ver / götür (saytdakı kimi: admin özünü admin rolundan çıxara bilməz — server yoxlayır).</summary>
    [RelayCommand]
    private async Task Manage(AdminUserItem item)
    {
        try { _roles ??= await _admin.RolesAsync(); }
        catch (ApiException ex) { await Alert(Lang.T("common_error"), ex.Message); return; }

        var u = item.User;
        var actions = _roles.Select(r => u.Roles.Contains(r) ? "− " + r : "+ " + r).ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync($"{u.FullName} (@{u.UserName})", Lang.T("common_cancel"), null, actions);
        if (choice == null || choice.Length < 3 || (choice[0] != '+' && choice[0] != '−')) return;

        var role = choice[2..];
        var add = choice[0] == '+';
        if (!await Confirm(Lang.T("adm_role_title"),
                Lang.T(add ? "adm_role_add_text" : "adm_role_remove_text", role, u.UserName), Lang.T("common_yes"))) return;

        await RunAsync(async () =>
        {
            if (add) await _admin.AddRoleAsync(u.Id, role);
            else await _admin.RemoveRoleAsync(u.Id, role);
            Items.SyncWith((await _admin.UsersAsync(Query)).Select(x => new AdminUserItem(x)));
        }, showAlert: true);
    }
}

/// <summary>İstifadəçi sətri (avatar ünvanı tam, rollar bir sətirdə).</summary>
public record AdminUserItem(AdminUserDto User)
{
    public string? AvatarUrl => AppConfig.Absolute(User.AvatarUrl);
    public string Initial => string.IsNullOrEmpty(User.FullName) ? "?" : User.FullName[..1].ToUpperInvariant();
    public string RolesText => User.Roles.Count == 0 ? "—" : string.Join(", ", User.Roles);
    public bool IsAdmin => User.Roles.Contains("Admin");
}

/// <summary>Məzmun bölməsinin siyahısı (slayderlər, ustalar, ...).</summary>
public partial class AdminSectionViewModel : BaseViewModel, IQueryAttributable
{
    private readonly AdminClient _admin;

    [ObservableProperty] private AdminSectionDto? section;
    [ObservableProperty] private string? createBlockedReason;
    [ObservableProperty] private bool isRefreshing;
    public ObservableCollection<AdminListItem> Items { get; } = new();

    public bool CanCreate => Section?.CanCreate == true;
    public bool IsEmpty => !IsBusy && Items.Count == 0;

    public AdminSectionViewModel(AdminClient admin)
    {
        _admin = admin;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(IsEmpty)); };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("section", out var s) && s is AdminSectionDto dto)
        {
            Section = dto;
            OnPropertyChanged(nameof(CanCreate));
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (Section == null) return;
        await RunAsync(async () =>
        {
            var list = await _admin.ListAsync(Section.Key);
            CreateBlockedReason = list.CreateBlockedReason;
            Items.SyncWith(list.Items.Select(i => new AdminListItem(i)));
        });
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task Add()
    {
        if (Section == null) return;
        if (!string.IsNullOrEmpty(CreateBlockedReason)) { await Alert(Section.Title, CreateBlockedReason); return; }
        await Shell.Current.GoToAsync("admin-form", new Dictionary<string, object> { ["section"] = Section });
    }

    [RelayCommand]
    private Task Open(AdminListItem item) =>
        Shell.Current.GoToAsync("admin-form", new Dictionary<string, object> { ["section"] = Section!, ["id"] = item.Item.Id });
}

public record AdminListItem(AdminItemDto Item)
{
    public string? ImageUrl => AppConfig.Absolute(Item.ImageUrl);
    public bool HasImage => !string.IsNullOrEmpty(Item.ImageUrl);
    public string Initial => string.IsNullOrEmpty(Item.Title) ? "•" : Item.Title[..1].ToUpperInvariant();
}
