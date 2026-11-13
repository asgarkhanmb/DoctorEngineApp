using CommunityToolkit.Mvvm.ComponentModel;
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

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>Əməliyyatı icra edir: yüklənmə göstəricisi + xəta mesajı (Azərbaycan dilində, serverdən).</summary>
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
            if (showAlert) await Alert("Xəta", ex.Message);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Gözlənilməz xəta baş verdi. Yenidən cəhd edin.";
            System.Diagnostics.Debug.WriteLine(ex);
            if (showAlert) await Alert("Xəta", ErrorMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected static Task Alert(string title, string message) =>
        Shell.Current?.DisplayAlertAsync(title, message, "Bağla") ?? Task.CompletedTask;

    protected static async Task<bool> Confirm(string title, string message, string ok = "Bəli") =>
        Shell.Current != null && await Shell.Current.DisplayAlertAsync(title, message, ok, "Ləğv et");

    protected static async Task OpenAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { await Launcher.Default.OpenAsync(new Uri(url)); }
        catch { await Alert("Açılmadı", "Bu linki açmaq mümkün olmadı."); }
    }
}
