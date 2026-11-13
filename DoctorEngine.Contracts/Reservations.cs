namespace DoctorEngine.Contracts;

// =====================================================
// Rezervasiya (api/v1/reservations)
// Saytdakı forma ilə eyni qaydalar: iş saatları, 30 dəq-lik saatlar, 10 gün irəli.
// =====================================================

public record BookableServiceDto(int Id, string Name);

/// <summary>Təqvimi tətbiqdə qurmaq üçün qaydalar (Bakı vaxtı).</summary>
public record ReservationRulesDto(int OpeningHour, int ClosingHour, int SlotMinutes, int MaxDaysAhead, string TimeZone);

public record ReservationOptionsDto(IReadOnlyList<BookableServiceDto> Services, ReservationRulesDto Rules);

/// <param name="Time">"10:30"</param>
/// <param name="State">"free" | "busy" | "past"</param>
public record SlotDto(string Time, string State);

/// <param name="DateTime">Bakı vaxtı ilə seçilmiş gün və saat.</param>
public record CreateReservationRequest(
    string FullName,
    string Phone,
    string WhatsApp,
    int ServiceId,
    DateTime DateTime,
    string? Note);

/// <param name="TrackingCode">Uğurlu olduqda statusu izləmək üçün kod (tətbiq yadda saxlayır).</param>
public record CreateReservationResponse(bool Success, string Message, string? TrackingCode);

/// <param name="Status">"Pending" | "Confirmed" | "Rejected"</param>
public record ReservationStatusDto(string Code, string Service, string Date, string Time, string Status, string? Reason);
