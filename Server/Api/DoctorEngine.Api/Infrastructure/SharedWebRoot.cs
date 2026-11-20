namespace DoctorEngine.Api.Infrastructure
{
    /// <summary>
    /// API-nin "wwwroot"-u saytın wwwroot qovluğudur — yüklənmiş şəkillər/videolar (uploads) bir yerdə saxlanılır,
    /// saytda admin şəkil yükləyəndə tətbiqdə də dərhal görünür (və əksinə).
    /// Ayar: "Storage:WebRoot" (appsettings, mühit dəyişəni Storage__WebRoot və ya --Storage:WebRoot=...).
    /// </summary>
    public static class SharedWebRoot
    {
        public static string Resolve(string[] args)
        {
            var contentRoot = Directory.GetCurrentDirectory();
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(contentRoot)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var path = config["Storage:WebRoot"];
            if (string.IsNullOrWhiteSpace(path))
                path = "wwwroot";

            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(contentRoot, path));
            Directory.CreateDirectory(full);
            return full;
        }
    }
}
