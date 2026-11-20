using DoctorEngineApp.ViewModels.ContactPhones;
using DoctorEngineApp.ViewModels.Infroms;

namespace DoctorEngineApp.ViewModels.Reservations
{
    public class ReservationPageVM
    {
        public ReservationPageVM()
        {
            Informs = new List<InformVM>();
            Reservation = new ReservationCreateVM();
            ContactPhone = new ContactPhoneVM();
        }

        public List<InformVM> Informs { get; set; }
        public ReservationCreateVM Reservation { get; set; }
        public ContactPhoneVM ContactPhone { get; set; }
    }
}
