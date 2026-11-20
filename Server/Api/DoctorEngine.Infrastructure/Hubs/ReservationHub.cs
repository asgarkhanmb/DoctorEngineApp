using Microsoft.AspNetCore.SignalR;

namespace DoctorEngineApp.Hubs
{
    /// <summary>
    /// Rezervasiyalar üçün real vaxt kanalı (/reservationHub).
    ///  - Hamı: boş saatların dəyişməsi (SlotTaken / SlotFreed)
    ///  - Admin qrupu: yeni rezervasiya və status dəyişiklikləri
    ///  - Müştəri: yalnız öz izləmə kodunun qrupu (StatusChanged)
    /// </summary>
    public class ReservationHub : Hub
    {
        public const string AdminsGroup = "res-admins";

        public static string TrackGroup(string code) => "res-" + code;

        public static bool IsValidCode(string? code) =>
            code is { Length: 32 } && code.All(Uri.IsHexDigit);

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole("Admin") == true)
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);

            await base.OnConnectedAsync();
        }

        /// <summary>Müştəri öz rezervasiyalarının statusunu izləyir (kodlar təxmin edilə bilməz).</summary>
        public async Task Track(string[] codes)
        {
            if (codes == null) return;

            foreach (var code in codes.Take(10))
            {
                if (IsValidCode(code))
                    await Groups.AddToGroupAsync(Context.ConnectionId, TrackGroup(code.ToLowerInvariant()));
            }
        }
    }
}
