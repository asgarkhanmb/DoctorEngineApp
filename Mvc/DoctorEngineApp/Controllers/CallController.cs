using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Controllers
{
    /// <summary>
    /// Video / səsli zəng səhifəsi (WebRTC). Brauzerdə ayrıca pəncərədə, mobil tətbiqdə WebView-da açılır.
    ///  /Call?c={yazışma}&amp;video=1 — zəng et;  /Call?id={zəng}&amp;video=1 — cavab ver.
    /// Giriş: sayt — cookie; tətbiq — token ünvanın "#t=" hissəsində (serverə getmir, yalnız /callHub-a ötürülür).
    /// </summary>
    [AllowAnonymous]
    public class CallController : Controller
    {
        private readonly IConfiguration _config;

        public CallController(IConfiguration config) => _config = config;

        [HttpGet("/Call")]
        public IActionResult Index()
        {
            // STUN/TURN serverləri: appsettings → "Calls:IceServers" (mobil internetdə TURN tövsiyə olunur)
            var ice = _config.GetSection("Calls:IceServers").Get<List<IceServer>>();
            if (ice == null || ice.Count == 0)
                ice = new List<IceServer> { new() { Urls = new[] { "stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302" } } };
            ViewData["IceServers"] = ice;
            Response.Headers["Cache-Control"] = "no-store";
            return View();
        }

        public class IceServer
        {
            public string[] Urls { get; set; } = Array.Empty<string>();
            public string? Username { get; set; }
            public string? Credential { get; set; }
        }
    }
}
