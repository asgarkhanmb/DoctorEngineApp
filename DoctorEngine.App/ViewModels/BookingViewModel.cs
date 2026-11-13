using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Rezervasiya — saytdakı forma ilə eyni axın: xidmət → gün → boş saat → əlaqə məlumatı.
/// Saatların vəziyyəti (boş / tutulub / keçib) serverdən gəlir.
/// </summary>
public partial class BookingViewModel : BaseViewModel, IQueryAttributable
{
    private static readonly string[] DayNames = { "B.", "B.e", "Ç.a", "Ç.", "C.a", "C.", "Ş." };
    private readonly BookingService _booking;
    private ReservationRulesDto? _rules;
    private int? _pendingServiceId;
    private bool _loaded;

    public ObservableCollection<BookableServiceDto> Services { get; } = new();
    public ObservableCollection<DayItem> Days { get; } = new();
    public ObservableCollection<SlotItem> Slots { get; } = new();
    public ObservableCollection<ReservationStatusDto> MyReservations { get; } = new();

    [ObservableProperty] private BookableServiceDto? selectedService;
    [ObservableProperty] private DayItem? selectedDay;
    [ObservableProperty] private SlotItem? selectedSlot;
    [ObservableProperty] private bool isLoadingSlots;
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string whatsApp = "";
    [ObservableProperty] private bool sameAsPhone = true;
    [ObservableProperty] private string? note;
    [ObservableProperty] private string? successMessage;
    [ObservableProperty] private string? successCode;

    public bool HasMyReservations => MyReservations.Count > 0;
    public bool NoSlots => !IsLoadingSlots && SelectedService != null && Slots.Count > 0 && Slots.All(s => !s.IsFree);
    public string Summary => SelectedService == null ? "Xidmət seçin"
        : SelectedSlot == null ? $"{SelectedService.Name} · saat seçin"
        : $"{SelectedService.Name} · {SelectedDay?.Long} · {SelectedSlot.Time}";

    public BookingViewModel(BookingService booking)
    {
        _booking = booking;
        MyReservations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMyReservations));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("serviceId", out var v) && int.TryParse(v?.ToString(), out var id))
        {
            _pendingServiceId = id;
            var match = Services.FirstOrDefault(s => s.Id == id);
            if (match != null) SelectedService = match;
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_loaded) { await LoadMineAsync(); return; }
        await RunAsync(async () =>
        {
            var options = await _booking.GetOptionsAsync();
            _rules = options.Rules;
            Services.Clear();
            foreach (var s in options.Services) Services.Add(s);

            BuildDays();
            FullName = _booking.LastName;
            Phone = _booking.LastPhone;
            WhatsApp = _booking.LastWhatsApp;
            SameAsPhone = string.IsNullOrEmpty(WhatsApp) || WhatsApp == Phone;

            SelectedService = Services.FirstOrDefault(s => s.Id == _pendingServiceId) ?? SelectedService;
            _loaded = true;
        });
        await LoadMineAsync();
    }

    private void BuildDays()
    {
        Days.Clear();
        var today = BakuNow().Date;
        var lastSlotToday = today.AddHours(_rules!.ClosingHour).AddMinutes(-_rules.SlotMinutes);
        var start = BakuNow() > lastSlotToday ? 1 : 0;   // bu günün saatları bitibsə — sabahdan
        var az = new CultureInfo("az-Latn-AZ");
        for (var i = start; i <= _rules.MaxDaysAhead; i++)
        {
            var d = today.AddDays(i);
            var label = i == 0 ? "Bu gün" : i == 1 ? "Sabah" : DayNames[(int)d.DayOfWeek];
            Days.Add(new DayItem(d, label, d.Day.ToString(), d.ToString("d MMMM", az)));
        }
        SelectedDay = Days.FirstOrDefault();
    }

    partial void OnSelectedServiceChanged(BookableServiceDto? value)
    {
        OnPropertyChanged(nameof(Summary));
        _ = LoadSlotsAsync();
    }

    partial void OnSelectedDayChanged(DayItem? value)
    {
        foreach (var d in Days) d.IsSelected = d == value;
        OnPropertyChanged(nameof(Summary));
        _ = LoadSlotsAsync();
    }

    partial void OnSelectedSlotChanged(SlotItem? value)
    {
        foreach (var s in Slots) s.IsSelected = s == value;
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnIsLoadingSlotsChanged(bool value) => OnPropertyChanged(nameof(NoSlots));

    [RelayCommand] private void SelectDay(DayItem day) => SelectedDay = day;

    [RelayCommand]
    private void SelectSlot(SlotItem slot)
    {
        if (slot.IsFree) SelectedSlot = slot;
    }

    private async Task LoadSlotsAsync()
    {
        if (SelectedService == null || SelectedDay == null) return;
        var keep = SelectedSlot?.Time;
        IsLoadingSlots = true;
        try
        {
            var slots = await _booking.GetSlotsAsync(SelectedService.Id, SelectedDay.Date);
            Slots.Clear();
            foreach (var s in slots) Slots.Add(new SlotItem(s.Time, s.State));
            SelectedSlot = Slots.FirstOrDefault(s => s.Time == keep && s.IsFree);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingSlots = false;
            OnPropertyChanged(nameof(NoSlots));
        }
    }

    [RelayCommand]
    private async Task Submit()
    {
        SuccessMessage = null;
        if (SelectedService == null) { ErrorMessage = "Xidmət seçin."; return; }
        if (SelectedDay == null || SelectedSlot == null) { ErrorMessage = "Gün və boş saat seçin."; return; }
        if (string.IsNullOrWhiteSpace(FullName)) { ErrorMessage = "Ad Soyad tələb olunur."; return; }
        if (string.IsNullOrWhiteSpace(Phone)) { ErrorMessage = "Telefon tələb olunur."; return; }
        var wa = SameAsPhone ? Phone : WhatsApp;
        if (string.IsNullOrWhiteSpace(wa)) { ErrorMessage = "WhatsApp nömrəsi tələb olunur."; return; }

        var parts = SelectedSlot.Time.Split(':');
        var when = SelectedDay.Date.AddHours(int.Parse(parts[0])).AddMinutes(int.Parse(parts[1]));

        await RunAsync(async () =>
        {
            var result = await _booking.CreateAsync(new CreateReservationRequest(
                FullName.Trim(), Phone.Trim(), wa.Trim(), SelectedService.Id, when, Note));

            _booking.LastName = FullName.Trim();
            _booking.LastPhone = Phone.Trim();
            _booking.LastWhatsApp = wa.Trim();

            SuccessMessage = result.Message;
            SuccessCode = result.TrackingCode;
            Note = null;
            SelectedSlot = null;
            await LoadSlotsAsync();
            await LoadMineAsync();
        }, showAlert: true);

        // Saat bu arada tutulubsa — siyahını yenilə
        if (HasError) await LoadSlotsAsync();
    }

    [RelayCommand]
    private async Task LoadMineAsync()
    {
        try
        {
            var mine = await _booking.GetMineAsync();
            MyReservations.Clear();
            foreach (var r in mine) MyReservations.Add(r);
        }
        catch (ApiException) { /* status siyahısı vacib deyil — sükutla keç */ }
    }

    /// <summary>Bakı vaxtı (UTC+4) — cihazın saat qurşağından asılı olmadan.</summary>
    private static DateTime BakuNow()
    {
        try { return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Baku"); }
        catch { return DateTime.UtcNow.AddHours(4); }
    }
}

public partial class DayItem : ObservableObject
{
    public DateTime Date { get; }
    public string Label { get; }
    public string Number { get; }
    public string Long { get; }
    [ObservableProperty] private bool isSelected;

    public DayItem(DateTime date, string label, string number, string longText)
    {
        Date = date; Label = label; Number = number; Long = longText;
    }
}

public partial class SlotItem : ObservableObject
{
    public string Time { get; }
    public string State { get; }
    public bool IsFree => State == "free";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background), nameof(Foreground))]
    private bool isSelected;

    /// <summary>Seçilmiş — tünd göy; boş — ağ; tutulub — açıq qırmızı; keçib — boz (saytdakı kimi).</summary>
    public Color Background => IsSelected
        ? Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#D81324" : "#0B2154")
        : State switch
    {
        "free" => Color.FromArgb("#FFFFFF"),
        "busy" => Color.FromArgb("#FDECEE"),
        _ => Color.FromArgb("#EEF0F4")
    };

    public Color Foreground => IsSelected ? Colors.White : State switch
    {
        "free" => Color.FromArgb("#0B2154"),
        "busy" => Color.FromArgb("#D81324"),
        _ => Color.FromArgb("#A3AAB8")
    };

    public SlotItem(string time, string state)
    {
        Time = time; State = state;
    }
}
