using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
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
    public bool IsAdmin => _auth.User?.IsAdmin == true;
    public string Initial => string.IsNullOrEmpty(User?.FullName) ? "?" : User!.FullName[..1].ToUpperInvariant();
    public string? AvatarUrl => AppConfig.Absolute(User?.AvatarUrl);
    public string AppVersion => Lang.T("profile_version", AppInfo.Current.VersionString);

    /// <summary>Dil seçimi (AZ / EN / RU / TR) — giriş etməmiş istifadəçiyə də görünür.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } = Lang.Supported.Select(l => new LanguageOption(l)).ToList();

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(AppVersion));
        foreach (var l in Languages) l.Refresh();
    }

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
        OnPropertyChanged(nameof(IsAdmin));
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
            throw new ApiException(Lang.T("profile_err_login"), System.Net.HttpStatusCode.BadRequest);
        await _auth.LoginAsync(LoginName, Password);
        Password = "";
        InfoMessage = null;
    });

    [RelayCommand]
    private Task Register() => RunAsync(async () =>
    {
        if (Password != Password2)
            throw new ApiException(Lang.T("profile_err_pw_mismatch"), System.Net.HttpStatusCode.BadRequest);
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
            throw new ApiException(Lang.T("profile_err_email"), System.Net.HttpStatusCode.BadRequest);
        InfoMessage = (await _auth.ForgotPasswordAsync(Email)).Message;
        Mode = "login";
    });

    [RelayCommand]
    private async Task Logout()
    {
        if (!await Confirm(Lang.T("profile_logout_title"), Lang.T("profile_logout_confirm"), Lang.T("profile_logout_ok"))) return;
        await _auth.LogoutAsync();
    }

    [RelayCommand]
    private async Task SaveServer()
    {
        AppConfig.BaseUrl = ServerUrl;
        ServerUrl = AppConfig.BaseUrl;
        _api.Reset();
        await Alert(Lang.T("profile_server_title"), Lang.T("profile_server_saved", AppConfig.BaseUrl));
    }
}

/// <summary>Dil düyməsi (Profil → Dil).</summary>
public partial class LanguageOption : ObservableObject
{
    public LanguageOption(LanguageInfo info) => Info = info;

    public LanguageInfo Info { get; }
    public string Code => Info.Code;
    public string Short => Info.Short;
    public string NativeName => Info.NativeName;
    public bool IsSelected => Lang.Current.Code == Info.Code;

    [RelayCommand] private void Select() => Lang.Current.Set(Info.Code);

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}
