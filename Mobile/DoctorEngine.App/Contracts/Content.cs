namespace DoctorEngine.Contracts;
// Tətbiqin öz DTO-ları (API cavablarının forması). API-də ayrıca surəti var: Api/DoctorEngine.Application/Contracts — dəyişəndə uyğunlaşdırın.

// =====================================================
// Sayt məzmunu (api/v1/content) — ana səhifə, xidmətlər, ustalar, əlaqə.
// Bütün şəkil / video ünvanları serverə nisbi yoldur ("/uploads/..."):
// tətbiq onları ApiClient.BaseAddress ilə birləşdirir.
// =====================================================

public record HomeDto(
    SiteDto Site,
    IReadOnlyList<SlideDto> Slides,
    IReadOnlyList<InformDto> Informs,
    AboutDto? About,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<TechnicianDto> Technicians,
    ContactDto? Contact,
    IReadOnlyList<ContactPhoneDto> Phones,
    MapDto? Map,
    WhatsAppDto? WhatsApp);

public record SiteDto(string Name, string? LogoIcon, string? LogoUrl);

public record SlideDto(int Id, string? Title, string? Subtitle, string? BackgroundUrl, string? SideImageUrl);

public record InformDto(string? Title, string? Description, string? IconUrl);

public record AboutDto(string? Title, string? Description, string? VideoUrl);

/// <param name="ReservationServiceId">Bu kateqoriya üçün rezervasiya xidmətinin Id-si (yoxdursa null).</param>
public record CategoryDto(int Id, string Name, string? IconUrl, IReadOnlyList<VideoDto> Videos, int? ReservationServiceId);

public record VideoDto(int Id, string? Title, string? Description, string? VideoUrl);

public record TechnicianDto(
    int Id,
    string FullName,
    string? Position,
    string? Bio,
    string? ImageUrl,
    string? FacebookUrl,
    string? InstagramUrl,
    string? TiktokUrl);

public record ContactDto(
    string? Address,
    string? Phone,
    string? Email,
    string? YouTube,
    string? Facebook,
    string? TikTok,
    string? WhatsApp,
    string? Weekdays,
    string? Weekend);

public record ContactPhoneDto(string? Title, string? Subtitle, string? Description, string? PhoneNumber);

public record MapDto(string? Address, string? MapUrl);

/// <param name="Link">Hazır wa.me linki (mesaj daxil).</param>
public record WhatsAppDto(string PhoneNumber, string? Message, string Link);

public record ContactMessageRequest(string FullName, string Email, string Subject, string Message);

/// <summary>"Haqqımızda" səhifəsi (saytdakı About/Index ilə eyni məlumat).</summary>
public record AboutPageDto(AboutDto? About, IReadOnlyList<InformDto> Informs, IReadOnlyList<TechnicianDto> Technicians, ContactDto? Contact);

/// <summary>"Əlaqə" səhifəsi (saytdakı Contact/Index ilə eyni məlumat).</summary>
public record ContactPageDto(ContactDto? Contact, MapDto? Map);

