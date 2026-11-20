using DoctorEngineApp.ViewModels.Categories;

namespace DoctorEngineApp.ViewModels.Services
{
    /// <summary>
    /// Xidmət kartları (Views/Shared/_ServiceGrid.cshtml) — Ana səhifə və Xidmətlər səhifəsi.
    /// </summary>
    public class ServiceGridVM
    {
        public List<CategoryVM> Categories { get; set; } = new();

        // Kateqoriya Id → rezervasiya formasındakı xidmət Id
        public Dictionary<int, int> ReservationServiceIds { get; set; } = new();

        // Kateqoriya filtri göstərilsin (Xidmətlər səhifəsi)
        public bool ShowFilter { get; set; }

        // Maksimum kart sayı (Ana səhifə üçün); null — hamısı
        public int? Limit { get; set; }

        /// <summary>
        /// Kateqoriya adı rezervasiya xidmətinin adı ilə eynidirsə, onları əlaqələndirir —
        /// "Rezervasiya et" düyməsi xidməti formada əvvəlcədən seçir.
        /// </summary>
        public static Dictionary<int, int> MapReservationIds(IEnumerable<CategoryVM>? categories, IEnumerable<ServiceVM>? services)
        {
            var map = new Dictionary<int, int>();
            if (categories == null || services == null)
                return map;

            var list = services.ToList();
            foreach (var cat in categories)
            {
                var match = list.FirstOrDefault(s =>
                    string.Equals(s.Name?.Trim(), cat.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    map[cat.Id] = match.Id;
            }
            return map;
        }
    }
}
