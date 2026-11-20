using DoctorEngineApp.Models;

namespace DoctorEngineApp.ViewModels.Admin
{
    public class DashboardVM
    {
        public int PendingReservations { get; set; }
        public int TodayReservations { get; set; }
        public int TotalReservations { get; set; }
        public int MessagesLast7Days { get; set; }
        public int TotalMessages { get; set; }
        public int ChatMessagesToday { get; set; }
        public int Users { get; set; }
        public int Warranties { get; set; }

        public List<DashboardReservationVM> UpcomingReservations { get; set; } = new();
        public List<DoctorEngineApp.Models.ContactMessage> LatestMessages { get; set; } = new();
        public List<ChatMessage> LatestChat { get; set; } = new();
    }

    public class DashboardReservationVM
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? WhatsApp { get; set; }
        public string ServiceName { get; set; } = "";
        public DateTime DateTime { get; set; }
        public string Status { get; set; } = "";
    }
}
