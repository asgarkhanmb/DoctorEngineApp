using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Services;
using DoctorEngine.Contracts;

namespace DoctorEngine.App.ViewModels;

/// <summary>Profil: giriş / qeydiyyat / şifrə bərpası və ya istifadəçi məlumatı + çıxış.</summary>
public partial class ProfileViewModel : BaseViewModel
{
    private readonly AuthService _auth;
    private readonly ApiClient _api;

    /// <summary>"login" | "register" | "forgot"</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginMode), nameof(IsRegisterMode), nameof(IsForgotMode))]
    private string mode = "login";

    [ObservableProperty] private string loginName = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password2 = "";
    [ObservableProperty] private string? infoMessage;
    [ObservableProperty] private string serverUrl = AppConfig.BaseUrl;

    public bool IsLoggedIn => _auth.IsLoggedIn;
    public bool IsGuest => !_auth.IsLoggedIn;
    public bool IsLoginMode => Mode == "login";
    public bool IsRegisterMode => Mode == "register";
    public bool IsForgotMode => Mode == "forgot";
    public bool HasInfo => !string.IsNullOrEmpty(InfoMessage);
    public UserDto? User => _auth.User;
    public string Initial => string.IsNullOrEmpty(User?.FullName) ? "?" : User!.FullName[..1].ToUpperInvariant();
    public string? AvatarUrl => AppConfig.Absolute(User?.AvatarUrl);
    public string AppVersion => $"Versiya {AppInfo.Current.VersionString}";

#if DEBUG
    public bool ShowDevSettings => true;
#else
    public bool ShowDevSettings => false;
#endif

    public ProfileViewModel(AuthService auth, ApiClient api)
    {
        _auth = auth;
        _api = api;
        _auth.Changed += (_, _) => Notify();
    }

    partial void OnInfoMessageChanged(string? value) => OnPropertyChanged(nameof(HasInfo));

    private void Notify()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(User));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(AvatarUrl));
    }

    public async Task AppearingAsync()
    {
        Notify();
        if (IsLoggedIn) { try { await _auth.RefreshProfileAsync(); } catch { } }
    }

    [RelayCommand] private void ShowLogin() { Mode = "login"; ErrorMessage = null; }
    [RelayCommand] private void ShowRegister() { Mode = "register"; ErrorMessage = null; InfoMessage = null; }
    [RelayCommand] private void ShowForgot() { Mode = "forgot"; ErrorMessage = null; InfoMessage = null; }

    [RelayCommand]
    private Task Login() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(LoginName) || string.IsNullOrEmpty(Password))
            throw new ApiException("İstifadəçi adı/email və şifrəni daxil edin.", System.Net.HttpStatusCode.BadRequest);
        await _auth.LoginAsync(LoginName, Password);
        Password = "";
        InfoMessage = null;
    });

    [RelayCommand]
    private Task Register() => RunAsync(async () =>
    {
        if (Password != Password2)
            throw new ApiException("Şifrələr eyni deyil.", System.Net.HttpStatusCode.BadRequest);
        var result = await _auth.RegisterAsync(new RegisterRequest(FullName.Trim(), UserName.Trim(), Email.Trim(), Password));
        InfoMessage = result.Message;
        LoginName = UserName.Trim();
        Password = Password2 = "";
        Mode = "login";
    });

    [RelayCommand]
    private Task Forgot() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Email))
            throw new ApiException("Email ünvanını daxil edin.", System.Net.HttpStatusCode.BadRequest);
        InfoMessage = (await _auth.ForgotPasswordAsync(Email)).Message;
        Mode = "login";
    });

    [RelayCommand]
    private async Task Logout()
    {
        if (!await Confirm("Çıxış", "Hesabdan çıxmaq istəyirsiniz?", "Çıx")) return;
        await _auth.LogoutAsync();
    }

    [RelayCommand]
    private async Task SaveServer()
    {
        AppConfig.BaseUrl = ServerUrl;
        ServerUrl = AppConfig.BaseUrl;
        _api.Reset();
        await Alert("Server", $"Server ünvanı: {AppConfig.BaseUrl}\nDəyişikliyin tam tətbiqi üçün tətbiqi yenidən açın.");
    }
}
