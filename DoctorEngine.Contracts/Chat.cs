namespace DoctorEngine.Contracts;

// =====================================================
// Canlı çat (api/chat — sayt və tətbiq eyni endpoint-ləri istifadə edir)
// Canlı yeniləmələr: SignalR hub "/chatHub" (token: ?access_token=...)
//   hadisələr: ReceiveMessage(ChatMessageDto), EditMessage(ChatMessageDto), DeleteMessage(int id)
// =====================================================

/// <param name="AttachmentType">"image" | "video" | "audio" | null</param>
/// <param name="Date">Bakı vaxtı, "yyyy-MM-dd"</param>
/// <param name="Time">Bakı vaxtı, "HH:mm"</param>
public record ChatMessageDto(
    int Id,
    string UserName,
    string? Message,
    bool IsAdmin,
    string? AttachmentUrl,
    string? AttachmentType,
    bool IsEdited,
    string Date,
    string Time);

public record ChatPageDto(IReadOnlyList<ChatMessageDto> Messages, bool HasMore);

public record UnreadDto(int Count);

/// <summary>Chat limitləri (saytla eyni).</summary>
public static class ChatLimits
{
    public const int MaxMessageLength = 1000;
    public const long MaxImageBytes = 10L * 1024 * 1024;
    public const long MaxVideoBytes = 100L * 1024 * 1024;
    public const long MaxAudioBytes = 16L * 1024 * 1024;
}

// =====================================================
// AI köməkçi (api/ai/chat) — cavab text/plain axını kimi hissə-hissə gəlir
// =====================================================

/// <param name="Role">"user" | "assistant"</param>
public record AiMessageDto(string Role, string Content);

public record AiChatRequest(IReadOnlyList<AiMessageDto> Messages);
