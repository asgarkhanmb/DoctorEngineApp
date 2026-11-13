using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DoctorEngineApp.Hubs
{
    // Yalnız daxil olmuş istifadəçilər qoşula bilər — qonaqlar canlı mesajları almır
    [Authorize]
    public class ChatHub : Hub
    {
    }
}
