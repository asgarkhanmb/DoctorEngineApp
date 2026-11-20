namespace DoctorEngine.App.Services;

/// <summary>
/// Telefonun bildiriş panelinə (Android) / Windows bildiriş mərkəzinə bildiriş göstərir.
/// Firebase (FCM) olmadan: tətbiq bildirişləri serverdən özü yoxlayır (açıqdırsa — 30 saniyədən bir,
/// bağlıdırsa Android-də WorkManager ilə ~15 dəqiqədən bir) və yenilərini burada göstərir.
/// Bildirişə toxunanda tətbiq açılır və həmin video (rəydirsə — rəylər) göstərilir.
/// </summary>
public static class LocalNotifier
{
    public const string ExtraReelId = "de_reel";
    public const string ExtraOpenComments = "de_comments";

#if ANDROID
    private const string ChannelId = "de_social";
#endif

    /// <summary>Android 13+ — bildiriş icazəsi (bir dəfə soruşulur).</summary>
    public static async Task RequestPermissionAsync()
    {
#if ANDROID
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
                await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch (Exception ex) { CrashLog.Write(ex, "Notify.Permission"); }
#else
        await Task.CompletedTask;
#endif
    }

    public static void Show(int id, string title, string body, int? reelId, bool openComments)
    {
        try
        {
#if ANDROID
            ShowAndroid(id, title, body, reelId, openComments);
#elif WINDOWS
            ShowWindows(title, body);
#endif
        }
        catch (Exception ex) { CrashLog.Write(ex, "Notify.Show"); }
    }

#if ANDROID
    private static void ShowAndroid(int id, string title, string body, int? reelId, bool openComments)
    {
        var context = global::Android.App.Application.Context;
        var manager = AndroidX.Core.App.NotificationManagerCompat.From(context)!;
        if (!manager.AreNotificationsEnabled()) return;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var system = (global::Android.App.NotificationManager)context.GetSystemService(global::Android.Content.Context.NotificationService)!;
            if (system.GetNotificationChannel(ChannelId) == null)
                system.CreateNotificationChannel(new global::Android.App.NotificationChannel(ChannelId,
                    Localization.Lang.T("notif_title"), global::Android.App.NotificationImportance.High)
                {
                    Description = Localization.Lang.T("notif_channel_desc")
                });
        }

        var intent = new global::Android.Content.Intent(context, typeof(MainActivity));
        intent.SetFlags(global::Android.Content.ActivityFlags.SingleTop | global::Android.Content.ActivityFlags.ClearTop);
        if (reelId is int r)
        {
            intent.PutExtra(ExtraReelId, r);
            intent.PutExtra(ExtraOpenComments, openComments);
        }
        var pending = global::Android.App.PendingIntent.GetActivity(context, id, intent,
            global::Android.App.PendingIntentFlags.UpdateCurrent | global::Android.App.PendingIntentFlags.Immutable);

        var notification = new AndroidX.Core.App.NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyChat)!
            .SetContentTitle(title)!
            .SetContentText(body)!
            .SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle().BigText(body))!
            .SetColor(unchecked((int)0xFFD81324))!
            .SetAutoCancel(true)!
            .SetPriority(AndroidX.Core.App.NotificationCompat.PriorityHigh)!
            .SetContentIntent(pending)!
            .Build();
        manager.Notify(id, notification);
    }
#endif

#if WINDOWS
    private static bool _registered;

    private static void ShowWindows(string title, string body)
    {
        if (!_registered)
        {
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();
            _registered = true;
        }
        var toast = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .BuildNotification();
        Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(toast);
    }
#endif
}
