using System.Net;
using System.Net.Http.Headers;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>"Videolar" lenti (api/v1/reels): lent, bəyənmə, rəylər, admin üçün paylaşma / silmə.</summary>
public class ReelsService
{
    /// <summary>Server limiti ilə eyni (ReelsController.MaxVideoBytes).</summary>
    public const long MaxVideoBytes = 500L * 1024 * 1024;

    private readonly ApiClient _api;

    public ReelsService(ApiClient api) => _api = api;

    public Task<ReelPageDto> FeedAsync(int? before = null, int take = 8, CancellationToken ct = default) =>
        _api.GetAsync<ReelPageDto>(before is > 0 ? $"api/v1/reels?take={take}&before={before}" : $"api/v1/reels?take={take}", ct);

    public Task<ReelLikeResultDto> LikeAsync(int id, bool like) =>
        _api.PostAsync<ReelLikeResultDto>($"api/v1/reels/{id}/like?like={(like ? "true" : "false")}", null);

    public async Task ViewAsync(int id)
    {
        try { await _api.PostAsync($"api/v1/reels/{id}/view", null); }
        catch { /* baxış sayı vacib deyil */ }
    }

    public Task<ReelCommentPageDto> CommentsAsync(int id, int? before = null) =>
        _api.GetAsync<ReelCommentPageDto>(before is > 0 ? $"api/v1/reels/{id}/comments?before={before}" : $"api/v1/reels/{id}/comments");

    /// <summary>Rəy / cavab (parentId) / stiker.</summary>
    public Task<ReelCommentDto> AddCommentAsync(int id, string? text, int? parentId = null, string? sticker = null) =>
        _api.PostAsync<ReelCommentDto>($"api/v1/reels/{id}/comments", new ReelCommentRequest(text, parentId, sticker));

    public Task<ReelLikeResultDto> LikeCommentAsync(int commentId, bool like) =>
        _api.PostAsync<ReelLikeResultDto>($"api/v1/reels/comments/{commentId}/like?like={(like ? "true" : "false")}", null);

    public Task<ReelCommentDto> EditCommentAsync(int commentId, string text) =>
        _api.PutAsync<ReelCommentDto>($"api/v1/reels/comments/{commentId}", new ReelCommentUpdateRequest(text));

    /// <summary>Bildirişdən açılan video.</summary>
    public Task<ReelDto> GetAsync(int id) => _api.GetAsync<ReelDto>($"api/v1/reels/{id}");

    public Task DeleteCommentAsync(int commentId) => _api.DeleteAsync($"api/v1/reels/comments/{commentId}");

    public Task DeleteAsync(int id) => _api.DeleteAsync($"api/v1/reels/{id}");

    public Task<ReelDto> UpdateCaptionAsync(int id, string? caption) =>
        _api.PutAsync<ReelDto>($"api/v1/reels/{id}", new ReelUpdateRequest(caption));

    /// <summary>Admin: video paylaş. <paramref name="progress"/> 0..1 — yüklənmə faizi.</summary>
    public Task<ReelDto> UploadAsync(FileResult file, string? caption, IProgress<double>? progress, CancellationToken ct = default) =>
        _api.UploadAsync<ReelDto>("api/v1/reels", () =>
        {
            var form = new MultipartFormDataContent();
            if (!string.IsNullOrWhiteSpace(caption)) form.Add(new StringContent(caption.Trim()), "caption");
            var video = new ProgressStreamContent(File.OpenRead(file.FullPath), progress);
            video.Headers.ContentType = new MediaTypeHeaderValue(ContentType(file.FileName));
            form.Add(video, "file", SafeName(file.FileName));
            return form;
        }, TimeSpan.FromMinutes(30), ct);

    public static string ContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".m4v" => "video/x-m4v",
        _ => "video/mp4"
    };

    // Bəzi telefonlar faylı uzantısız / qəribə adla qaytarır — server uzantıya baxır
    private static string SafeName(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".mp4" or ".mov" or ".m4v" or ".webm" ? "video" + ext : "video.mp4";
    }

    /// <summary>Fayl axını — göndərilən baytları sayır (yüklənmə zolağı üçün).</summary>
    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly IProgress<double>? _progress;

        public ProgressStreamContent(Stream source, IProgress<double>? progress)
        {
            _source = source;
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var total = _source.Length;
            var buffer = new byte[81920];
            long sent = 0;
            int read;
            double last = -1;
            while ((read = await _source.ReadAsync(buffer)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read));
                sent += read;
                var p = total > 0 ? (double)sent / total : 0;
                if (p - last >= 0.01 || sent == total) { last = p; _progress?.Report(p); }
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _source.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }
}
