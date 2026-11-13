namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationResult
    {
        public bool Success { get; private init; }
        public string Message { get; private init; } = string.Empty;

        // Uğurlu olduqda: müştərinin statusu canlı izləməsi üçün kod
        public string? TrackingCode { get; private init; }

        public static ReservationResult Ok(string message, string? trackingCode = null) =>
            new() { Success = true, Message = message, TrackingCode = trackingCode };
        public static ReservationResult Fail(string message) => new() { Success = false, Message = message };
    }

    /// <summary>Gün üçün bir saat: "10:30" + vəziyyət (free / busy / past).</summary>
    public record ReservationSlot(string Time, string State);

    /// <summary>Müştəriyə göstərilən status (şəxsi məlumat olmadan).</summary>
    public record ReservationStatusVM(
        string Code,
        string Service,
        string Date,
        string Time,
        string Status,
        string? Reason);
}
