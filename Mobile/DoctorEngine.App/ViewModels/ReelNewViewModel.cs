using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;

namespace DoctorEngine.App.ViewModels;

/// <summary>Admin: yeni video paylaşmaq (qalereyadan seç və ya çək, mətn yaz, yüklə).</summary>
public partial class ReelNewViewModel : BaseViewModel
{
    private readonly ReelsService _reels;
    private readonly ReelsViewModel _feed;
    private CancellationTokenSource? _cts;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasFile))] private FileResult? file;
    [ObservableProperty] private string? previewPath;
    [ObservableProperty] private string? fileInfo;
    [ObservableProperty] private string? caption;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(UploadText))] private double progress;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isUploading;

    public bool HasFile => File != null;
    public bool CanEdit => !IsUploading;
    public string UploadText => Lang.T("reels_uploading", (int)Math.Round(Progress * 100));

    public ReelNewViewModel(ReelsService reels, ReelsViewModel feed)
    {
        _reels = reels;
        _feed = feed;
    }

    [RelayCommand]
    private async Task Pick()
    {
        try { Use((await MediaPicker.Default.PickVideosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault()); }
        catch (Exception ex) { CrashLog.Write(ex, "ReelNew.Pick"); await Alert(Lang.T("common_error"), ex.Message); }
    }

    [RelayCommand]
    private async Task Record()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported) { await Pick(); return; }
            Use(await MediaPicker.Default.CaptureVideoAsync());
        }
        catch (Exception ex) { CrashLog.Write(ex, "ReelNew.Record"); await Alert(Lang.T("common_error"), ex.Message); }
    }

    private async void Use(FileResult? picked)
    {
        if (picked == null) return;
        var size = new FileInfo(picked.FullPath).Length;
        if (size > ReelsService.MaxVideoBytes)
        {
            await Alert(Lang.T("common_error"), Lang.T("reels_too_big"));
            return;
        }
        File = picked;
        PreviewPath = picked.FullPath;
        FileInfo = $"{picked.FileName} · {size / 1024d / 1024d:0.#} MB";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task Publish()
    {
        if (IsUploading) return;
        if (File == null) { ErrorMessage = Lang.T("reels_pick_first"); return; }

        IsUploading = true;
        Progress = 0;
        ErrorMessage = null;
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(p => Progress = p);   // UI axınında yaradılıb — hesabat UI-da gəlir
            var dto = await _reels.UploadAsync(File, Caption, progress, _cts.Token);
            _feed.Published(dto);
            await Shell.Current.GoToAsync("..");
            await Shell.Current.GoToAsync("//reels");
        }
        catch (OperationCanceledException) { /* istifadəçi dayandırdı */ }
        catch (ApiException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex) { CrashLog.Write(ex, "ReelNew.Publish"); ErrorMessage = Lang.T("common_unexpected"); }
        finally
        {
            IsUploading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelUpload() => _cts?.Cancel();

    public void Leaving() => _cts?.Cancel();
}
