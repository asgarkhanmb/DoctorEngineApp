namespace DoctorEngine.Contracts;
// "Videolar" lenti (api/v1/reels) və bildirişlər (api/v1/notifications).
// API-dəki surəti: Server/Api/DoctorEngine.Application/Contracts/Reels.cs — dəyişəndə ikisini də dəyişin.

/// <param name="VideoUrl">Serverə nisbi yol ("/uploads/reels/...").</param>
/// <param name="CreatedAt">ISO tarix (yyyy-MM-ddTHH:mm:ss, Bakı vaxtı).</param>
/// <param name="Liked">Cari istifadəçi bəyənibmi (girişsiz — false).</param>
/// <param name="CanDelete">Cari istifadəçi admindirsə — true.</param>
public record ReelDto(int Id, string VideoUrl, string? Caption, string Author, string? AuthorAvatarUrl, string CreatedAt,
                      int Likes, int Comments, int Views, bool Liked, bool CanDelete);

public record ReelPageDto(IReadOnlyList<ReelDto> Items, bool HasMore);

public record ReelLikeResultDto(bool Liked, int Likes);

/// <param name="ParentId">Cavabdırsa — yuxarı səviyyəli rəyin Id-si.</param>
/// <param name="Sticker">Stiker kodu (<see cref="ReelStickers"/>) — mətn əvəzinə.</param>
/// <param name="ReplyToName">Cavab yazılan şəxsin adı ("@ad" kimi göstərilir).</param>
/// <param name="Replies">Yuxarı səviyyəli rəyin cavabları (köhnədən yeniyə).</param>
/// <param name="Likes">Rəyin bəyənmə sayı; Liked — cari istifadəçi bəyənibmi.</param>
public record ReelCommentDto(int Id, int? ParentId, string UserName, string FullName, string? AvatarUrl, string Text,
                             string? Sticker, string? ReplyToName, string CreatedAt, bool Edited, bool CanEdit, bool CanDelete,
                             IReadOnlyList<ReelCommentDto> Replies, int Likes = 0, bool Liked = false);

/// <param name="Total">Bütün rəylərin sayı (cavablar daxil).</param>
public record ReelCommentPageDto(IReadOnlyList<ReelCommentDto> Items, bool HasMore, int Total);

/// <param name="ParentId">Rəyə cavab — həmin rəyin Id-si (cavaba cavab da ola bilər).</param>
/// <param name="Sticker">Mətn əvəzinə stiker.</param>
public record ReelCommentRequest(string? Text, int? ParentId = null, string? Sticker = null);

public record ReelCommentUpdateRequest(string? Text);

public record ReelUpdateRequest(string? Caption);

/// <summary>Rəylərdə göndərilə bilən stikerlər: kod → emoji (tətbiq böyük ölçüdə göstərir).</summary>
public static class ReelStickers
{
    public static readonly IReadOnlyList<(string Code, string Emoji)> All =
    [
        ("fire", "🔥"), ("love", "😍"), ("clap", "👏"), ("laugh", "😂"),
        ("wow", "😮"), ("cry", "😢"), ("heart", "❤️"), ("like", "👍"),
        ("hundred", "💯"), ("car", "🚗"), ("wrench", "🔧"), ("race", "🏁")
    ];

    public static string? Emoji(string? code) => All.FirstOrDefault(s => s.Code == code).Emoji;
}

// =====================================================
// Bildirişlər (api/v1/notifications)
// =====================================================

/// <param name="Type">reel_like | reel_comment | comment_reply | comment_like — tətbiq mətni seçilmiş dildə qurur.</param>
/// <param name="Snippet">Rəyin qısa mətni (stikerdirsə — emoji).</param>
public record NotificationDto(int Id, string Type, string Actor, string ActorName, string? ActorAvatarUrl,
                              int? ReelId, int? CommentId, string? Snippet, bool IsRead, string CreatedAt);

public record NotificationPageDto(IReadOnlyList<NotificationDto> Items, bool HasMore, int Unread);

public record UnreadCountDto(int Count);
