namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Admin paneldə kateqoriya üçün seçilə bilən hazır ikonlar.
    /// Fayllar: wwwroot/img/category-icons/{Key}.png (128×128, şəffaf fon, brend rəngi).
    /// Seçilən ikon uploads/categories qovluğuna kopyalanır — sayt, API və mobil tətbiq
    /// ikonları əvvəlki kimi oradan oxuyur.
    /// </summary>
    public static class CategoryIconPresets
    {
        public const string WebFolder = "img/category-icons";

        public record Preset(string Key, string Label);

        public static readonly IReadOnlyList<Preset> All = new List<Preset>
        {
            new("screwdriver-wrench", "Təmir"),
            new("wrench", "Açar"),
            new("toolbox", "Alətlər"),
            new("hammer", "Çəkic"),
            new("oil-can", "Yağ dəyişmə"),
            new("droplet", "Mayelər"),
            new("filter", "Filtr"),
            new("gas-pump", "Yanacaq"),
            new("car", "Avtomobil"),
            new("car-side", "Avtomobil (yan)"),
            new("car-on", "Mühərrik işə salma"),
            new("truck-pickup", "Pikap / yük"),
            new("car-burst", "Kuzov / qəza"),
            new("spray-can-sparkles", "Rəngləmə"),
            new("paint-roller", "Boya"),
            new("soap", "Yuma"),
            new("gears", "Mühərrik / sürətlər qutusu"),
            new("gauge-high", "Cihazlar paneli"),
            new("stethoscope", "Diaqnostika"),
            new("magnifying-glass", "Yoxlama"),
            new("clipboard-check", "Texniki baxış"),
            new("microchip", "Elektronika / ECU"),
            new("laptop-code", "Kompüter diaqnostikası"),
            new("car-battery", "Akkumulyator"),
            new("bolt", "Elektrik"),
            new("plug", "Elektrik naqilləri"),
            new("lightbulb", "İşıqlar"),
            new("fire", "Alışma sistemi"),
            new("snowflake", "Kondisioner"),
            new("fan", "Soyutma / ventilyator"),
            new("temperature-high", "Hərarət"),
            new("key", "Açar / immobilayzer"),
            new("shield-halved", "Zəmanət / qoruma"),
            new("road", "Yol / təkər"),
        };

        public static bool IsValid(string? key) =>
            !string.IsNullOrEmpty(key) && All.Any(p => p.Key == key);

        public static string Url(string key) => $"~/{WebFolder}/{key}.png";
    }

    /// <summary>_IconPicker partial-ın modeli.</summary>
    /// <param name="SelectedPreset">Seçilmiş hazır ikon (forma xəta ilə qayıdanda).</param>
    /// <param name="ExistingIcon">Redaktədə hazırkı ikonun fayl adı (uploads/categories).</param>
    /// <param name="Required">Yeni kateqoriyada ikon məcburidir.</param>
    public record CategoryIconPicker(string? SelectedPreset, string? ExistingIcon, bool Required);
}
