using Android.Webkit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace DoctorEngine.App.Platforms.Android;

/// <summary>
/// Zəng səhifəsi (WebRTC) üçün WebView: səhifə kamera / mikrofon istəyəndə icazə verilir
/// (sistem icazəsi əvvəlcədən CallClient.EnsurePermissionsAsync ilə alınır).
/// MAUI-nin öz klientindən törənir — Navigating hadisəsi işləməyə davam edir.
/// </summary>
public class CallChromeClient : MauiWebChromeClient
{
    public CallChromeClient(WebViewHandler handler) : base(handler) { }

    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request == null) return;
        var allowed = request.GetResources()?
            .Where(r => r == PermissionRequest.ResourceVideoCapture || r == PermissionRequest.ResourceAudioCapture)
            .ToArray() ?? Array.Empty<string>();
        if (allowed.Length > 0) request.Grant(allowed);
        else request.Deny();
    }
}

#if TESTBUILD
/// <summary>Yalnız test APK: kompüterdəki lokal saytın HTTPS sertifikatı etibarlı deyil — qəbul edilir.</summary>
public class TestSslWebViewClient : MauiWebViewClient
{
    public TestSslWebViewClient(WebViewHandler handler) : base(handler) { }

    public override void OnReceivedSslError(global::Android.Webkit.WebView? view, SslErrorHandler? handler, global::Android.Net.Http.SslError? error)
        => handler?.Proceed();
}
#endif
