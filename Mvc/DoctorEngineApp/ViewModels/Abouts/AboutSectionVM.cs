using DoctorEngineApp.ViewModels.Infroms;

namespace DoctorEngineApp.ViewModels.Abouts
{
    /// <summary>
    /// "Haqqımızda" bloku (Views/Shared/_AboutHero.cshtml) — Ana səhifə və Haqqımızda səhifəsi.
    /// IsHome: Ana səhifədə ikinci düymə "Daha ətraflı" (Haqqımızda səhifəsinə) olur.
    /// </summary>
    public record AboutSectionVM(
        string? Title,
        string? Description,
        string? VideoPath,
        List<InformVM>? Informs,
        bool IsHome = false);
}
