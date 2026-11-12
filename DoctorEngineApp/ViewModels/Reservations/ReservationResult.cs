namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationResult
    {
        public bool Success { get; private init; }
        public string Message { get; private init; } = string.Empty;

        public static ReservationResult Ok(string message) => new() { Success = true, Message = message };
        public static ReservationResult Fail(string message) => new() { Success = false, Message = message };
    }
}
