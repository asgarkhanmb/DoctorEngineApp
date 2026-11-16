using CommunityToolkit.Mvvm.ComponentModel;
using DoctorEngine.App.Localization;
using DoctorEngine.App.Services;

namespace DoctorEngine.App.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty] private string? errorMessage;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    protected BaseViewModel()
    {
        // Zəif abunə (Lang.Changed WeakEventManager-dir) — keçici ViewModel-lər yaddaşda qalmır
        Lang.Current.Changed += OnLangChanged;
    }

    private void OnLangChanged(object? sender, EventArgs e)
    {
        ErrorMessage = null;   // köhnə dildəki xəta mətni qalmasın
        OnLanguageChanged();
    }

    /// <summary>Dil dəyişəndə: kodda yaradılan mətnləri (salamlama, status və s.) yenidən hesablayın.</summary>
    protected virtual void OnLanguageChanged() { }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>Əməliyyatı icra edir: yüklənmə göstəricisi + xəta mesajı.</summary>
    protected async Task RunAsync(Func<Task> action, bool showAlert = false)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            if (showAlert) await Alert(Lang.T("common_error"), ex.Message);
        }
        catch (Exception ex)
        {
            ErrorMessage = Lang.T("common_unexpected");
            System.Diagnostics.Debug.WriteLine(ex);
            if (showAlert) await Alert(Lang.T("common_error"), ErrorMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected static Task Alert(string title, string message) =>
        Shell.Current?.DisplayAlertAsync(title, message, Lang.T("common_close")) ?? Task.CompletedTask;

    protected static async Task<bool> Confirm(string title, string message, string? ok = null) =>
        Shell.Current != null && await Shell.Current.DisplayAlertAsync(title, message, ok ?? Lang.T("common_yes"), Lang.T("common_cancel"));

    protected static async Task OpenAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { await Launcher.Default.OpenAsync(new Uri(url)); }
        catch { await Alert(Lang.T("common_open_failed_title"), Lang.T("common_open_failed")); }
    }
}
