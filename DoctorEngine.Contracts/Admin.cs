namespace DoctorEngine.Contracts;

// =====================================================
// Profil redaktəsi (api/v1/profile) — saytın "Profilim" səhifəsi ilə eyni qaydalar
// =====================================================

public record UpdateProfileRequest(string FullName, string UserName);

/// <summary>Email dərhal dəyişmir: yeni ünvana təsdiq linki göndərilir (saytdakı kimi).</summary>
public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// =====================================================
// Mobil admin panel (api/v1/admin) — yalnız "Admin" rolu
// =====================================================

public record AdminDashboardDto(
    int PendingReservations,
    int TodayReservations,
    int TotalReservations,
    int MessagesLast7Days,
    int TotalMessages,
    int ChatMessagesToday,
    int Users,
    int Warranties,
    IReadOnlyList<AdminReservationDto> Upcoming);

public record AdminReservationDto(
    int Id,
    string FullName,
    string Phone,
    string? WhatsApp,
    string ServiceName,
    DateTime DateTime,
    string? Note,
    string Status,
    string? RejectionReason);

public record RejectReservationRequest(string Reason);

public record AdminMessageDto(int Id, string FullName, string Email, string Subject, string Message, DateTime CreatedDate);

public record AdminUserDto(
    string Id,
    string UserName,
    string FullName,
    string Email,
    string? AvatarUrl,
    bool EmailConfirmed,
    bool IsLockedOut,
    IReadOnlyList<string> Roles);

public record RoleRequest(string Role);

// ---------- Məzmun bölmələri (slayder, xidmət, usta, ...) ----------
// Formalar serverdən gələn sxemlə qurulur: tətbiq hər bölmə üçün ayrıca ekran saxlamır.

/// <param name="Kind">"list" — siyahı (əlavə et / redaktə / sil); "single" — tək qeyd (ayarlar).</param>
public record AdminSectionDto(
    string Key,
    string Title,
    string? Subtitle,
    string Group,
    string Kind,
    bool CanCreate,
    bool CanEdit,
    bool CanDelete);

/// <param name="Type">text | multiline | email | url | phone | number | bool | image | video | select | items</param>
/// <param name="Mode">"all" | "create" (yalnız yeni qeyddə) | "edit" (yalnız redaktədə)</param>
public record AdminFieldDto(
    string Name,
    string Label,
    string Type,
    bool Required = false,
    string? Hint = null,
    int? MaxLength = null,
    IReadOnlyList<AdminOptionDto>? Options = null,
    string Mode = "all");

public record AdminOptionDto(string Value, string Label);

public record AdminItemDto(int Id, string Title, string? Subtitle, string? ImageUrl, string? Badge);

public record AdminListDto(IReadOnlyList<AdminItemDto> Items, string? CreateBlockedReason);

/// <param name="Id">null — yeni qeyd.</param>
/// <param name="Values">Sahə adı → hazırkı dəyər (mətn).</param>
/// <param name="Files">Şəkil / video sahəsi → hazırkı faylın ünvanı (önizləmə üçün).</param>
/// <param name="ReadOnly">true — yalnız baxış (məs. zəmanət redaktə olunmur).</param>
public record AdminFormDto(
    int? Id,
    IReadOnlyList<AdminFieldDto> Fields,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, string?> Files,
    bool ReadOnly = false);

/// <summary>Zəmanət detalı ("items" tipli sahə): form-a Items[0].PartName, Items[0].WarrantyMonth kimi göndərilir.</summary>
public record AdminWarrantyItemDto(string PartName, int WarrantyMonth);
