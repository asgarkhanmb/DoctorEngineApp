using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Rezervasiya — saytdakı forma ilə eyni axın: xidmət → gün → boş saat → əlaqə məlumatı.
/// Saatların vəziyyəti (boş / tutulub / keçib) serverdən gəlir.
/// </summary>
public partial class BookingViewModel : BaseViewModel, IQueryAttributable
{
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
    public string Summary => SelectedService == null ? Lang.T("booking_choose_service")
        : SelectedSlot == null ? Lang.T("booking_summary_pick_time", SelectedService.Name)
        : $"{SelectedService.Name} · {SelectedDay?.Long} · {SelectedSlot.Time}";

    // ---- Mərhələ göstəricisi (başlıqda 1-2-3-4) ----
    public bool Step1Done => SelectedService != null;
    public bool Step2Done => SelectedService != null && SelectedDay != null;
    public bool Step3Done => SelectedSlot != null;
    public bool Step4Done => !string.IsNullOrWhiteSpace(FullName) && !string.IsNullOrWhiteSpace(Phone);
    public bool CanSubmit => Step1Done && Step3Done && Step4Done && !IsBusy;

    private static readonly string[] StepSources =
        { nameof(SelectedService), nameof(SelectedDay), nameof(SelectedSlot), nameof(FullName), nameof(Phone), nameof(IsBusy) };

    public BookingViewModel(BookingService booking)
    {
        _booking = booking;
        MyReservations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMyReservations));
        PropertyChanged += (_, e) =>
        {
            if (Array.IndexOf(StepSources, e.PropertyName) < 0) return;
            OnPropertyChanged(nameof(Step1Done)); OnPropertyChanged(nameof(Step2Done));
            OnPropertyChanged(nameof(Step3Done)); OnPropertyChanged(nameof(Step4Done));
            OnPropertyChanged(nameof(CanSubmit));
        };
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
        for (var i = start; i <= _rules.MaxDaysAhead; i++)
            Days.Add(new DayItem(today.AddDays(i), i));
        SelectedDay = Days.FirstOrDefault();
    }

    protected override void OnLanguageChanged()
    {
        foreach (var d in Days) d.RefreshText();
        OnPropertyChanged(nameof(Summary));
        // Status mətni konverterdən gəlir — siyahını yenidən çək
        MyReservations.Rebind();
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
            // Clear() yox — Windows-da BindableLayout Reset alanda çökə bilir (bax CollectionSync)
            Slots.SyncWith(slots.Select(s => new SlotItem(s.Time, s.State)));
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
        if (SelectedService == null) { ErrorMessage = Lang.T("booking_err_service"); return; }
        if (SelectedDay == null || SelectedSlot == null) { ErrorMessage = Lang.T("booking_err_slot"); return; }
        if (string.IsNullOrWhiteSpace(FullName)) { ErrorMessage = Lang.T("booking_err_name"); return; }
        if (string.IsNullOrWhiteSpace(Phone)) { ErrorMessage = Lang.T("booking_err_phone"); return; }
        var wa = SameAsPhone ? Phone : WhatsApp;
        if (string.IsNullOrWhiteSpace(wa)) { ErrorMessage = Lang.T("booking_err_wa"); return; }

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
            MyReservations.SyncWith(mine);
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
    private readonly int _offset;   // 0 — bu gün, 1 — sabah, ...

    public DateTime Date { get; }
    public string Number { get; }
    [ObservableProperty] private bool isSelected;

    /// <summary>"Bu gün" / "Sabah" / həftənin günü (qısa) — seçilmiş dildə.</summary>
    public string Label => _offset switch
    {
        0 => Lang.T("day_today"),
        1 => Lang.T("day_tomorrow"),
        _ => Lang.T("days_short").Split(',')[(int)Date.DayOfWeek]
    };

    /// <summary>"15 noyabr" / "15 November" / "15 ноября" / "15 Kasım".</summary>
    public string Long => Date.ToString("d MMMM", Lang.Current.Culture);

    public DayItem(DateTime date, int offset)
    {
        Date = date;
        _offset = offset;
        Number = date.Day.ToString(CultureInfo.InvariantCulture);
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Long));
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

    private static bool Dark => Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>Seçilmiş — qırmızı; boş — açıq kart; tutulub — açıq qırmızı (üstündən xətt); keçib — boz.</summary>
    public Color Background => IsSelected ? Color.FromArgb("#D81324") : State switch
    {
        "free" => Color.FromArgb(Dark ? "#202A44" : "#F1F4FA"),
        "busy" => Color.FromArgb(Dark ? "#3A1E26" : "#FDECEE"),
        _ => Color.FromArgb(Dark ? "#171E31" : "#F4F5F8")
    };

    public Color Foreground => IsSelected ? Colors.White : State switch
    {
        "free" => Color.FromArgb(Dark ? "#EEF1F7" : "#0B2154"),
        "busy" => Color.FromArgb(Dark ? "#FF8A95" : "#D81324"),
        _ => Color.FromArgb(Dark ? "#4B5670" : "#B4BAC6")
    };

    public TextDecorations Decorations => State == "free" ? TextDecorations.None : TextDecorations.Strikethrough;

    public SlotItem(string time, string state)
    {
        Time = time; State = state;
    }
}
