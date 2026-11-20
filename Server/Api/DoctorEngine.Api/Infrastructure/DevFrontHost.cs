using Microsoft.AspNetCore.StaticFiles;

namespace DoctorEngine.Api.Infrastructure;

/// <summary>
/// Yalnız Development: API işə düşəndə JS front-u (Server/Front) da http://localhost:5021-də açır.
/// Ayrı port saxlanılır ki, front production-dakı kimi API-yə CORS + Bearer ilə müraciət etsin.
/// Ünvanlar köhnə MVC saytı ilə eynidir: /About → About/index.html, tapılmayan → Error/404/index.html.
/// Ayarlar: "Front:Serve" (standart true), "Front:Path" (standart ../../Front), "Front:Port" (standart 5021).
/// Serverdə front IIS-dədir (Server/Front/web.config) — bu servis orada işləmir.
/// </summary>
public sealed class DevFrontHost(IConfiguration config, IHostEnvironment env, ILogger<DevFrontHost> log) : IHostedService
{
    private WebApplication? _front;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Front:Path"] ?? Path.Combine("..", "..", "Front")));
        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            log.LogWarning("Front tapılmadı: {Root} — 'Front:Path' ayarını yoxlayın.", root);
            return;
        }
        var port = config.GetValue("Front:Port", 5021);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = root });
        builder.WebHost.UseUrls($"http://localhost:{port}");   // API-nin ASPNETCORE_URLS-ini götürməsin
        builder.Logging.ClearProviders();
        _front = builder.Build();

        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".webmanifest"] = "application/manifest+json";
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        _front.Run(async ctx =>
        {
            var file = Resolve(ctx.Request.Path.Value ?? "/");
            var status = StatusCodes.Status200OK;
            if (file == null)
            {
                file = Path.Combine(root, "Error", "404", "index.html");
                status = StatusCodes.Status404NotFound;
            }
            if (!types.TryGetContentType(file, out var contentType)) contentType = "application/octet-stream";
            if (contentType.StartsWith("text/") || contentType.EndsWith("javascript") || contentType.EndsWith("json"))
                contentType += "; charset=utf-8";

            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers.CacheControl = "no-cache";
            await ctx.Response.SendFileAsync(file);
        });

        string? Resolve(string urlPath)
        {
            var full = Path.GetFullPath(Path.Combine(root, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) && !full.Equals(root, StringComparison.OrdinalIgnoreCase))
                return null;   // ../ qorunması
            if (File.Exists(full)) return full;
            var index = Path.Combine(full, "index.html");
            return File.Exists(index) ? index : null;
        }

        await _front.StartAsync(cancellationToken);
        log.LogInformation("JS front: http://localhost:{Port}  ({Root})", port, root);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_front == null) return;
        await _front.StopAsync(cancellationToken);
        await _front.DisposeAsync();
    }
}
