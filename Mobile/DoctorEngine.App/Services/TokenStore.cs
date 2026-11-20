using System.Text.Json;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>
/// Tokenlərin təhlükəsiz saxlanması: SecureStorage
/// (Android Keystore, iOS/macOS Keychain, Windows DPAPI). Heç vaxt adi faylda saxlanmır.
/// </summary>
public class TokenStore
{
    private const string AccessKey = "de_access";
    private const string AccessExpKey = "de_access_exp";
    private const string RefreshKey = "de_refresh";
    private const string UserKey = "de_user";

    public string? AccessToken { get; private set; }
    public DateTimeOffset AccessTokenExpiresAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public UserDto? User { get; private set; }

    public bool HasSession => !string.IsNullOrEmpty(RefreshToken);

    public async Task LoadAsync()
    {
        try
        {
            AccessToken = await SecureStorage.Default.GetAsync(AccessKey);
            RefreshToken = await SecureStorage.Default.GetAsync(RefreshKey);
            var exp = await SecureStorage.Default.GetAsync(AccessExpKey);
            AccessTokenExpiresAt = DateTimeOffset.TryParse(exp, out var e) ? e : DateTimeOffset.MinValue;
            var user = await SecureStorage.Default.GetAsync(UserKey);
            User = string.IsNullOrEmpty(user) ? null : JsonSerializer.Deserialize<UserDto>(user, Json.Options);
        }
        catch
        {
            // Bəzi Android cihazlarında açar dəyişəndə (məs. bərpadan sonra) oxumaq mümkün olmur — təmiz başla
            Clear();
        }
    }

    public async Task SaveAsync(AuthResponse auth)
    {
        AccessToken = auth.AccessToken;
        AccessTokenExpiresAt = auth.AccessTokenExpiresAt;
        RefreshToken = auth.RefreshToken;
        User = auth.User;
        await SecureStorage.Default.SetAsync(AccessKey, auth.AccessToken);
        await SecureStorage.Default.SetAsync(AccessExpKey, auth.AccessTokenExpiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync(RefreshKey, auth.RefreshToken);
        await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(auth.User, Json.Options));
    }

    public async Task SaveUserAsync(UserDto user)
    {
        User = user;
        await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(user, Json.Options));
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        User = null;
        AccessTokenExpiresAt = DateTimeOffset.MinValue;
        SecureStorage.Default.Remove(AccessKey);
        SecureStorage.Default.Remove(AccessExpKey);
        SecureStorage.Default.Remove(RefreshKey);
        SecureStorage.Default.Remove(UserKey);
    }
}

public static class Json
{
    /// <summary>Server ASP.NET Core standartı: camelCase.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
