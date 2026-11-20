namespace DoctorEngine.Contracts;
// Tətbiqin öz DTO-ları (API cavablarının forması). API-də ayrıca surəti var: Api/DoctorEngine.Application/Contracts — dəyişəndə uyğunlaşdırın.

// =====================================================
// Şəxsi çat (WhatsApp kimi) — api/chat (sayt və tətbiq eyni endpoint-ləri istifadə edir)
// Müştəri yalnız öz yazışmasını görür; usta (admin) "conversation" ilə müştərini seçir.
// Canlı yeniləmələr: SignalR hub "/chatHub" (token: ?access_token=...)
//   server → klient: ReceiveMessage(ChatMessageDto), EditMessage(ChatMessageDto), DeleteMessage(int id),
//                    Receipt(ChatReceiptDto), Typing(ChatTypingDto), Presence(ChatPresenceDto),
//                    ConversationUpdated(ChatConversationDto), ConversationRemoved(string)
//   klient → server: Typing(string? conversation, bool isTyping), Heartbeat()
// =====================================================

/// <summary>Cavab verilən mesajın qısa görünüşü.</summary>
public record ChatReplyDto(int Id, string UserName, bool IsAdmin, string? Message, string? AttachmentType, bool Deleted);

/// <summary>Bir emoji və onu qoyan istifadəçilər.</summary>
public record ChatReactionDto(string Emoji, IReadOnlyList<string> Users);

/// <param name="Conversation">Yazışma (müştərinin istifadəçi adı)</param>
/// <param name="AttachmentType">"image" | "video" | "audio" | null</param>
/// <param name="Date">Bakı vaxtı, "yyyy-MM-dd"</param>
/// <param name="Time">Bakı vaxtı, "HH:mm"</param>
public record ChatMessageDto(
    int Id,
    string Conversation,
    string UserName,
    string? Message,
    bool IsAdmin,
    string? AttachmentUrl,
    string? AttachmentType,
    bool IsEdited,
    string Date,
    string Time,
    ChatReplyDto? ReplyTo,
    IReadOnlyList<ChatReactionDto>? Reactions);

/// <summary>
/// Qarşı tərəf (müştəri üçün ustalar, usta üçün müştəri).
/// ReadUpToId / DeliveredUpToId — mənim mesajlarımdan hansıları oxuyub / alıb (mavi ✓✓ / ✓✓).
/// </summary>
public record ChatPeerDto(
    string Conversation,
    string Title,
    string? AvatarUrl,
    bool Online,
    string? LastSeen,
    int ReadUpToId,
    int DeliveredUpToId,
    bool IsGroup = false,
    int MemberCount = 0,
    bool CanWrite = true,
    bool AdminsOnly = false,
    bool AllUsers = false,
    bool IsDirect = false);

public record ChatPageDto(IReadOnlyList<ChatMessageDto> Messages, bool HasMore, ChatPeerDto? Peer);

/// <summary>Ustanın "Çatlar" siyahısında bir sətir.</summary>
public record ChatConversationDto(
    string Conversation,
    string Title,
    string? AvatarUrl,
    bool Online,
    string? LastSeen,
    int LastMessageId,
    string? LastMessage,
    bool LastFromAdmin,
    string LastDate,
    int Unread,
    bool LastDelivered,
    bool LastRead,
    bool IsGroup = false,
    string? LastSender = null,
    bool IsSupport = false,
    bool IsDirect = false);

/// <summary>İstifadəçi (qrupa üzv seçmək üçün).</summary>
public record ChatUserDto(string UserName, string FullName, string? AvatarUrl, bool IsAdmin, bool Online);

/// <summary>Qrupun məlumatı və üzvləri (yazışma açarı "g:{Id}").</summary>
public record ChatGroupInfoDto(
    string Conversation,
    int Id,
    string Name,
    string? Description,
    bool AllUsers,
    bool AdminsOnly,
    string CreatedBy,
    string CreatedDate,
    int MemberCount,
    IReadOnlyList<ChatUserDto> Members);

public record BroadcastResultDto(int Count);

/// <summary>Gələn zəng (/callHub → IncomingCall).</summary>
public record IncomingCallDto(string CallId, string Conversation, bool IsGroup, bool Video, string From, string FromName, bool FromIsAdmin, string Title);

public record CallRefDto(string CallId);

/// <param name="Side">"admin" — ustalar aldı/oxudu; "user" — müştəri aldı/oxudu</param>
public record ChatReceiptDto(string Conversation, string Side, int DeliveredUpToId, int ReadUpToId);

public record ChatTypingDto(string Conversation, bool FromAdmin, string UserName, bool IsTyping, bool IsGroup = false);

/// <param name="IsSupport">true — ustaların vəziyyəti (müştərilər üçün); false — müştərinin (ustalar üçün)</param>
public record ChatPresenceDto(string? Conversation, bool IsSupport, bool Online, string? LastSeen);

/// <summary>"api/chat/direct" cavabı: açılacaq yazışmanın açarı.</summary>
public record DirectResultDto(string Conversation);

/// <summary>"ConversationCleared": yazışma silindi (forEveryone — usta hamı üçün sildi).</summary>
public record ConversationClearedDto(string Conversation, bool ForEveryone);

public record UnreadDto(int Count);

/// <summary>Reaksiya üçün icazəli emojilər (serverlə eyni).</summary>
public static class ChatReactions
{
    public static readonly string[] Quick = { "👍", "❤️", "😂", "😮", "😢", "🙏" };
    public static readonly string[] All =
        { "👍", "❤️", "😂", "😮", "😢", "🙏", "👏", "🔥", "🎉", "😍", "🤔", "👌", "✅", "🔧", "🚗" };
}

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
