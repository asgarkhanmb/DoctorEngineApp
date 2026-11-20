namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Azərbaycan vaxtı (UTC+4). Serverin saat qurşağından və əməliyyat sistemindən
    /// (Windows/Linux) asılı olmadan düzgün işləyir.
    /// </summary>
    public static class AzTime
    {
        public static readonly TimeZoneInfo Zone = ResolveZone();

        /// <summary>Hazırkı Bakı vaxtı (Kind = Unspecified).</summary>
        public static DateTime Now =>
            DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone), DateTimeKind.Unspecified);

        private static TimeZoneInfo ResolveZone()
        {
            foreach (var id in new[] { "Azerbaijan Standard Time", "Asia/Baku" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            // Azərbaycan 2016-dan bəri yay vaxtına keçmir: sabit UTC+4
            return TimeZoneInfo.CreateCustomTimeZone("AZ", TimeSpan.FromHours(4), "Azerbaijan", "Azerbaijan");
        }
    }
}
