using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace DoctorEngine.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OpenFromNotification(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenFromNotification(intent);
    }

    /// <summary>Bildiriş panelindən açılıb — həmin videonu (rəydirsə — rəyləri) göstər.</summary>
    private static void OpenFromNotification(Intent? intent)
    {
        var reelId = intent?.GetIntExtra(Services.LocalNotifier.ExtraReelId, 0) ?? 0;
        if (reelId <= 0) return;
        var comments = intent!.GetBooleanExtra(Services.LocalNotifier.ExtraOpenComments, false);
        intent.RemoveExtra(Services.LocalNotifier.ExtraReelId);

        // Shell hazır olsun deyə bir az gözlə (soyuq açılışda)
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                for (var i = 0; i < 20 && Shell.Current == null; i++) await Task.Delay(150);
                if (Shell.Current == null) return;
                var services = IPlatformApplication.Current!.Services;
                await Shell.Current.GoToAsync("//reels");
                await services.GetRequiredService<ViewModels.ReelsViewModel>().ShowReelAsync(reelId, comments);
                _ = services.GetRequiredService<ViewModels.NotificationsViewModel>().RefreshCountAsync();
            }
            catch (Exception ex) { Services.CrashLog.Write(ex, "OpenFromNotification"); }
        });
    }
}
