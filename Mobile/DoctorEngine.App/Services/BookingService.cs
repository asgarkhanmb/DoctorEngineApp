using System.Text.Json;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>
/// Rezervasiya. Saytdakı kimi giriş tələb olunmur; izləmə kodları cihazda saxlanılır
/// ("Mənim rezervasiyalarım" — statuslar serverdən canlı yoxlanılır).
/// </summary>
public class BookingService
{
    private const string CodesKey = "reservation_codes";
    private readonly ApiClient _api;

    public BookingService(ApiClient api) => _api = api;

    public Task<ReservationOptionsDto> GetOptionsAsync() =>
        _api.GetAsync<ReservationOptionsDto>("api/v1/reservations/options");

    public Task<List<SlotDto>> GetSlotsAsync(int serviceId, DateTime day) =>
        _api.GetAsync<List<SlotDto>>($"api/v1/reservations/slots?serviceId={serviceId}&date={day:yyyy-MM-dd}");

    public async Task<CreateReservationResponse> CreateAsync(CreateReservationRequest request)
    {
        var result = await _api.PostAsync<CreateReservationResponse>("api/v1/reservations", request);
        if (result.Success && !string.IsNullOrEmpty(result.TrackingCode))
        {
            var codes = Codes;
            codes.Insert(0, result.TrackingCode);
            Codes = codes.Distinct().Take(20).ToList();
        }
        return result;
    }

    public async Task<List<ReservationStatusDto>> GetMineAsync()
    {
        var codes = Codes;
        if (codes.Count == 0) return new();
        var query = string.Join("&", codes.Select(c => "codes=" + Uri.EscapeDataString(c)));
        return await _api.GetAsync<List<ReservationStatusDto>>("api/v1/reservations/status?" + query);
    }

    public List<string> Codes
    {
        get
        {
            try { return JsonSerializer.Deserialize<List<string>>(Preferences.Default.Get(CodesKey, "[]")) ?? new(); }
            catch { return new(); }
        }
        private set => Preferences.Default.Set(CodesKey, JsonSerializer.Serialize(value));
    }

    // Formanın son dəyərləri (növbəti dəfə yenidən yazmamaq üçün)
    public string LastName { get => Preferences.Default.Get("res_name", ""); set => Preferences.Default.Set("res_name", value); }
    public string LastPhone { get => Preferences.Default.Get("res_phone", ""); set => Preferences.Default.Set("res_phone", value); }
    public string LastWhatsApp { get => Preferences.Default.Get("res_wa", ""); set => Preferences.Default.Set("res_wa", value); }
}
