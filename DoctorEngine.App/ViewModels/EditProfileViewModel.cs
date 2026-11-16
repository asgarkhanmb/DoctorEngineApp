using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;

namespace DoctorEngine.App.ViewModels;

/// <summary>
/// Profili redaktə et — saytın "Profilim" səhifəsi ilə eyni: şəkil, ad / istifadəçi adı,
/// email (yeni ünvana təsdiq linki gedir), şifrə.
/// </summary>
public partial class EditProfileViewModel : BaseViewModel
{
    private readonly AuthService _auth;

    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string newEmail = "";
    [ObservableProperty] private string emailPassword = "";
    [ObservableProperty] private string currentPassword = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string newPassword2 = "";

    public string? AvatarUrl => AppConfig.Absolute(_auth.User?.AvatarUrl);
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);
    public string Initial => string.IsNullOrEmpty(_auth.User?.FullName) ? "?" : _auth.User!.FullName[..1].ToUpperInvariant();
    public string CurrentEmail => _auth.User?.Email ?? "";

    // AuthService.Changed-ə abunə olmuruq: bu ViewModel keçicidir (Transient) — tək nüsxəli servis onu yaddaşda saxlamasın.
    // Şəkil / ad dəyişəndən sonra NotifyUser() özümüz çağırırıq.
    public EditProfileViewModel(AuthService auth) => _auth = auth;

    /// <summary>Ekran açılanda formanı hazırkı məlumatla doldurur.</summary>
    public void Load()
    {
        FullName = _auth.User?.FullName ?? "";
        UserName = _auth.User?.UserName ?? "";
        NewEmail = EmailPassword = CurrentPassword = NewPassword = NewPassword2 = "";
        ErrorMessage = null;
        NotifyUser();
    }

    private void NotifyUser()
    {
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(CurrentEmail));
    }

    [RelayCommand]
    private Task SaveInfo() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(UserName))
            throw new ApiException(Lang.T("pe_err_name"), System.Net.HttpStatusCode.BadRequest);
        await _auth.UpdateProfileAsync(FullName, UserName);
        NotifyUser();
        await Alert(Lang.T("pe_done"), Lang.T("pe_info_saved"));
    }, showAlert: true);

    [RelayCommand]
    private Task ChangeEmail() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(NewEmail) || string.IsNullOrEmpty(EmailPassword))
            throw new ApiException(Lang.T("pe_err_email"), System.Net.HttpStatusCode.BadRequest);
        var result = await _auth.ChangeEmailAsync(NewEmail, EmailPassword);
        NewEmail = EmailPassword = "";
        await Alert(Lang.T("pe_done"), result.Message);
    }, showAlert: true);

    [RelayCommand]
    private Task ChangePassword() => RunAsync(async () =>
    {
        if (string.IsNullOrEmpty(CurrentPassword) || string.IsNullOrEmpty(NewPassword))
            throw new ApiException(Lang.T("pe_err_password"), System.Net.HttpStatusCode.BadRequest);
        if (NewPassword != NewPassword2)
            throw new ApiException(Lang.T("profile_err_pw_mismatch"), System.Net.HttpStatusCode.BadRequest);
        var result = await _auth.ChangePasswordAsync(CurrentPassword, NewPassword);
        CurrentPassword = NewPassword = NewPassword2 = "";
        await Alert(Lang.T("pe_done"), result.Message);
    }, showAlert: true);

    [RelayCommand]
    private async Task ChangeAvatar()
    {
        string pick = Lang.T("pe_photo_pick"), take = Lang.T("pe_photo_take"), remove = Lang.T("pe_photo_remove");
        var options = new List<string> { pick };
        if (MediaPicker.Default.IsCaptureSupported) options.Add(take);
        if (HasAvatar) options.Add(remove);

        var choice = await Shell.Current.DisplayActionSheetAsync(Lang.T("pe_photo"), Lang.T("common_cancel"),
            HasAvatar ? remove : null, options.Where(o => o != remove).ToArray());
        if (choice == null || choice == Lang.T("common_cancel")) return;

        if (choice == remove)
        {
            await RunAsync(() => _auth.RemoveAvatarAsync(), showAlert: true);
            NotifyUser();
            return;
        }

        FileResult? photo = null;
        try
        {
            photo = choice == take
                ? await MediaPicker.Default.CapturePhotoAsync()
                : (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault();
        }
        catch (Exception ex) { CrashLog.Write(ex, "EditProfile.Photo"); }
        if (photo == null) return;

        await RunAsync(() => _auth.UploadAvatarAsync(photo), showAlert: true);
        NotifyUser();
    }
}
