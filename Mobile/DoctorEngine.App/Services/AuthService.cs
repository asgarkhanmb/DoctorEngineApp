using DoctorEngine.Contracts;

namespace DoctorEngine.App.Services;

/// <summary>Giriş vəziyyəti — bütün ekranlar buna abunə olur (Profil, Çat).</summary>
public class AuthService
{
    private readonly ApiClient _api;

    public event EventHandler? Changed;

    public AuthService(ApiClient api)
    {
        _api = api;
        _api.SessionExpired += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public UserDto? User => _api.Tokens.User;
    public bool IsLoggedIn => _api.Tokens.HasSession;

    public Task InitializeAsync() => _api.Tokens.LoadAsync();

    public async Task LoginAsync(string login, string password)
    {
        await _api.LoginAsync(new LoginRequest(login.Trim(), password, AppConfig.DeviceName));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<MessageResponse> RegisterAsync(RegisterRequest request) =>
        _api.PostAsync<MessageResponse>("api/v1/auth/register", request);

    public Task<MessageResponse> ForgotPasswordAsync(string email) =>
        _api.PostAsync<MessageResponse>("api/v1/auth/forgot-password", new ForgotPasswordRequest(email.Trim()));

    public async Task RefreshProfileAsync()
    {
        if (!IsLoggedIn) return;
        var me = await _api.GetAsync<UserDto>("api/v1/auth/me");
        await _api.Tokens.SaveUserAsync(me);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ================= PROFİL REDAKTƏSİ (api/v1/profile) =================

    public async Task UpdateProfileAsync(string fullName, string userName)
    {
        var user = await _api.PutAsync<UserDto>("api/v1/profile", new UpdateProfileRequest(fullName.Trim(), userName.Trim()));
        await _api.ForceRefreshAsync();   // tokendəki istifadəçi adı da yenilənsin (çat bunu istifadə edir)
        await SetUserAsync(user);
    }

    public Task<MessageResponse> ChangeEmailAsync(string newEmail, string currentPassword) =>
        _api.PostAsync<MessageResponse>("api/v1/profile/email", new ChangeEmailRequest(newEmail.Trim(), currentPassword));

    public async Task<MessageResponse> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var result = await _api.PostAsync<MessageResponse>("api/v1/profile/password", new ChangePasswordRequest(currentPassword, newPassword));
        await _api.ForceRefreshAsync();   // şifrə dəyişəndə köhnə access token etibarsız olur
        return result;
    }

    public async Task UploadAvatarAsync(FileResult photo)
    {
        var user = await _api.PostFormAsync<UserDto>("api/v1/profile/avatar", () =>
        {
            var form = new MultipartFormDataContent();
            var content = new StreamContent(File.OpenRead(photo.FullPath));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrEmpty(photo.ContentType) ? "image/jpeg" : photo.ContentType);
            form.Add(content, "photo", photo.FileName);
            return form;
        });
        await SetUserAsync(user);
    }

    public async Task RemoveAvatarAsync() => await SetUserAsync(await _api.DeleteAsync<UserDto>("api/v1/profile/avatar"));

    private async Task SetUserAsync(UserDto user)
    {
        await _api.Tokens.SaveUserAsync(user);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
