using System.Runtime.CompilerServices;
using System.Text;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>AI köməkçi — saytla eyni endpoint (api/ai/chat); cavab hissə-hissə (axın) gəlir.</summary>
public class AiClient
{
    private readonly ApiClient _api;

    public AiClient(ApiClient api) => _api = api;

    public async IAsyncEnumerable<string> AskAsync(IReadOnlyList<AiMessageDto> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var res = await _api.PostStreamAsync("api/ai/chat", new AiChatRequest(history), ct);
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[1024];
        var chars = new char[2048];
        int read;
        while ((read = await stream.ReadAsync(bytes, ct)) > 0)
        {
            // Decoder çoxbaytlı hərfləri (ə, ş, ğ) hissələr arasında düzgün birləşdirir
            var n = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            if (n > 0) yield return new string(chars, 0, n);
        }
    }
}
