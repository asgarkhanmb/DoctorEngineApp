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
}
