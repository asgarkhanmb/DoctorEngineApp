namespace DoctorEngine.Contracts;
// Bu fayl API-nin öz DTO-larıdır (cavab formaları). Mobil tətbiqdə ayrıca surəti var: DoctorEngine.App/Contracts.

// =====================================================
// Giriş / qeydiyyat (api/v1/auth)
// Tətbiq qısa ömürlü access token (JWT) + uzun ömürlü refresh token istifadə edir.
// =====================================================

/// <param name="DeviceName">Məs. "Pixel 8 (Android)" — profil səhifəsində aktiv cihazları göstərmək üçün.</param>
public record LoginRequest(string EmailOrUsername, string Password, string? DeviceName = null);

public record RegisterRequest(string FullName, string UserName, string Email, string Password);

public record RefreshRequest(string RefreshToken, string? DeviceName = null);

public record LogoutRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);

public record UserDto(
    string Id,
    string UserName,
    string FullName,
    string Email,
    string? AvatarUrl,
    IReadOnlyList<string> Roles)
{
    public bool IsAdmin => Roles.Contains("Admin");
}

/// <summary>Bütün API xətalarının vahid forması (sayt API-si ilə eyni: { error, code }).</summary>
public record ApiError(string Error, int Code, IReadOnlyDictionary<string, string[]>? Errors = null);

public record MessageResponse(string Message);

// ----- Email linkləri (saytdakı Account/ConfirmEmail, ResetPassword, ConfirmEmailChange ilə eyni) -----

public record ConfirmEmailRequest(string UserId, string Token);

/// <summary>Şifrə yenilənəndən sonra sayt kimi avtomatik daxil olunur (AuthResponse qaytarılır).</summary>
public record ResetPasswordRequest(string Email, string Token, string Password, string? DeviceName = null);

public record ConfirmEmailChangeRequest(string UserId, string Email, string Token);

// ----- Dil (saytdakı Language/Set, Language/Js) -----

public record LanguageDto(string Code, string Name);
