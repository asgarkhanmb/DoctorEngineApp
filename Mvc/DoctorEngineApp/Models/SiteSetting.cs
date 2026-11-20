using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.Models
{
    /// <summary>Saytın ümumi ayarları (tək qeyd): ad və loqo. Admin → Sayt ayarları.</summary>
    public class SiteSetting : BaseEntity
    {
        [Required]
        [MaxLength(60)]
        public string SiteName { get; set; } = SiteDefaults.Name;

        /// <summary>Font Awesome ikon sinfi (yalnız SiteDefaults.Icons siyahısından).</summary>
        [Required]
        [MaxLength(60)]
        public string LogoIcon { get; set; } = SiteDefaults.Icon;

        /// <summary>Yüklənmiş loqo şəkli (uploads/site/…). Varsa ikonun yerinə göstərilir.</summary>
        [MaxLength(120)]
        public string? LogoImage { get; set; }
    }

    public static class SiteDefaults
    {
        public const string Name = "DoctorEngine.Az";
        public const string Icon = "fa-solid fa-car";

        /// <summary>Seçilə bilən ikonlar (avtomobil / servis mövzusunda).</summary>
        public static readonly (string Css, string Label)[] Icons =
        {
            ("fa-solid fa-car", "Maşın"),
            ("fa-solid fa-car-side", "Maşın (yan)"),
            ("fa-solid fa-car-rear", "Maşın (arxa)"),
            ("fa-solid fa-truck-pickup", "Pikap"),
            ("fa-solid fa-gears", "Dişlilər"),
            ("fa-solid fa-gear", "Dişli"),
            ("fa-solid fa-screwdriver-wrench", "Alətlər"),
            ("fa-solid fa-wrench", "Açar"),
            ("fa-solid fa-toolbox", "Alət qutusu"),
            ("fa-solid fa-oil-can", "Yağ"),
            ("fa-solid fa-gauge-high", "Spidometr"),
            ("fa-solid fa-bolt", "İldırım"),
            ("fa-solid fa-fire-flame-curved", "Alov"),
            ("fa-solid fa-car-battery", "Akkumulyator"),
            ("fa-solid fa-gas-pump", "Yanacaq"),
            ("fa-solid fa-road", "Yol"),
            ("fa-solid fa-shield-halved", "Qalxan"),
            ("fa-solid fa-star", "Ulduz"),
            ("fa-solid fa-heart-pulse", "Diaqnostika"),
            ("fa-solid fa-stethoscope", "Stetoskop"),
            ("fa-solid fa-microchip", "Elektronika"),
            ("fa-solid fa-temperature-half", "Temperatur"),
            ("fa-solid fa-circle-nodes", "Sistem"),
            ("fa-solid fa-flag-checkered", "Finiş")
        };

        public static bool IsAllowedIcon(string? css) => css != null && Icons.Any(i => i.Css == css);
    }
}
