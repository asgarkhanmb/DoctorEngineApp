using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using DoctorEngine.App.ViewModels;

namespace DoctorEngine.App.Platforms.Android;

/// <summary>
/// Tətbiq bağlı olanda da yeni bildirişləri yoxlayır (~15 dəqiqədən bir — Android-in WorkManager minimumu)
/// və telefonun bildiriş panelinə çıxarır. Proses yoxdursa Android onu başladır: MauiApplication xidmətləri
/// (ApiClient, saxlanmış sessiya) qurur, burada eyni NotificationsViewModel istifadə olunur.
/// </summary>
[Register("az.doctorengine.app.NotificationsWorker")]
public class NotificationsWorker : Worker
{
    private const string UniqueName = "de-notifications";

    public NotificationsWorker(Context context, WorkerParameters parameters) : base(context, parameters) { }

    public override Result DoWork()
    {
        try
        {
            // Tətbiq ekrandadırsa — 30 saniyəlik yoxlama onsuz da işləyir
            if (App.IsInForeground) return Result.InvokeSuccess()!;
            var vm = IPlatformApplication.Current?.Services.GetService<NotificationsViewModel>();
            vm?.ShowNewInPanelAsync(show: true).GetAwaiter().GetResult();
        }
        catch (Exception ex) { Services.CrashLog.Write(ex, "NotificationsWorker"); }
        return Result.InvokeSuccess()!;
    }

    /// <summary>Bir dəfə planlaşdırılır (təkrar çağırılsa mövcud olan saxlanılır).</summary>
    public static void Schedule()
    {
        try
        {
            var constraints = new Constraints.Builder().SetRequiredNetworkType(NetworkType.Connected!).Build();
            var request = (PeriodicWorkRequest)new PeriodicWorkRequest.Builder(
                    Java.Lang.Class.FromType(typeof(NotificationsWorker)), 15, Java.Util.Concurrent.TimeUnit.Minutes!)
                .SetConstraints(constraints)
                .Build();
            WorkManager.GetInstance(global::Android.App.Application.Context)
                .EnqueueUniquePeriodicWork(UniqueName, ExistingPeriodicWorkPolicy.Keep!, request);
        }
        catch (Exception ex) { Services.CrashLog.Write(ex, "NotificationsWorker.Schedule"); }
    }
}
