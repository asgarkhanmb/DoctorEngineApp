using Microsoft.Extensions.Localization;

namespace DoctorEngineApp.Localization
{
    /// <summary>
    /// Validasiya mesajlarını ([Required(ErrorMessage = "...")], [Display(Name = "...")]) L.T ilə tərcümə edir —
    /// ViewModel-lərdəki azərbaycanca mətnlər dəyişmədən qalır, istifadəçi öz dilində görür.
    /// </summary>
    public class LStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name]
        {
            get
            {
                var value = L.T(name);
                return new LocalizedString(name, value, resourceNotFound: false);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var value = L.T(name, arguments);
                return new LocalizedString(name, value, resourceNotFound: false);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    public class LStringLocalizerFactory : IStringLocalizerFactory
    {
        private static readonly LStringLocalizer Instance = new();
        public IStringLocalizer Create(Type resourceSource) => Instance;
        public IStringLocalizer Create(string baseName, string location) => Instance;
    }
}
