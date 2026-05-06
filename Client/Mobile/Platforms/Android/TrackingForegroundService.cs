#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Mobile.Models;
using Mobile.Services;

namespace Mobile.Platforms.Android;

[Service(Exported = false, Name = "com.companyname.mobile.TrackingForegroundService", ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class TrackingForegroundService : Service
{
    private const int PersistentNotificationId = 7001;
    private const string ChannelId = "tourguide_tracking";
    private CancellationTokenSource? _cts;

    public static void Start()
    {
        var context = Platform.AppContext;
        var intent = new Intent(context, typeof(TrackingForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    public static void Stop()
    {
        var context = Platform.AppContext;
        context.StopService(new Intent(context, typeof(TrackingForegroundService)));
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateChannel();
        StartForeground(PersistentNotificationId, BuildPersistentNotification());

        if (_cts == null)
        {
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cts.Token));
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDestroy();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var monitor = MainApplication.ServiceProvider?.GetRequiredService<GeofenceMonitorService>();
        if (monitor == null)
        {
            return;
        }

        await monitor.RunAsync(ShowPoiNotificationAsync, cancellationToken);
    }

    private Task ShowPoiNotificationAsync(PoiModel poi)
    {
        if (string.IsNullOrWhiteSpace(poi.Id))
        {
            return Task.CompletedTask;
        }

        var intent = new Intent(this, typeof(MainActivity));
        intent.SetAction("OPEN_POI");
        intent.PutExtra(App.PendingPoiNavigationKey, poi.Id);
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var notificationId = GetNotificationId(poi.Id);
        var pendingIntent = PendingIntent.GetActivity(
            this,
            notificationId,
            intent,
            GetPendingIntentFlags());

        if (pendingIntent == null)
        {
            return Task.CompletedTask;
        }

        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetContentTitle("B\u1ea1n \u0111ang g\u1ea7n m\u1ed9t qu\u00e1n trong TourGuide");
        builder.SetContentText($"M\u1edf thuy\u1ebft minh cho {poi.Name}");
        builder.SetContentIntent(pendingIntent);
        builder.SetAutoCancel(true);
        builder.SetPriority(NotificationCompat.PriorityHigh);

        var notification = builder.Build();

        if (notification != null)
        {
            NotificationManagerCompat.From(this)?.Notify(notificationId, notification);
        }

        return Task.CompletedTask;
    }

    private Notification BuildPersistentNotification()
    {
        var intent = new Intent(this, typeof(MainActivity));
        var pendingIntent = PendingIntent.GetActivity(
            this,
            PersistentNotificationId,
            intent,
            GetPendingIntentFlags());

        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetSmallIcon(Resource.Mipmap.appicon);
        builder.SetContentTitle("TourGuide \u0111ang theo d\u00f5i v\u1ecb tr\u00ed");
        builder.SetContentText("\u1ee8ng d\u1ee5ng s\u1ebd th\u00f4ng b\u00e1o khi b\u1ea1n \u0111\u1ebfn g\u1ea7n qu\u00e1n.");
        builder.SetOngoing(true);
        builder.SetPriority(NotificationCompat.PriorityLow);

        if (pendingIntent != null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        return builder.Build()!;
    }

    private void CreateChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var channel = new NotificationChannel(
            ChannelId,
            "TourGuide tracking",
            NotificationImportance.Default)
        {
            Description = "Theo d\u00f5i v\u1ecb tr\u00ed \u0111\u1ec3 nh\u1eafc thuy\u1ebft minh POI.",
        };

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }

    private static PendingIntentFlags GetPendingIntentFlags()
    {
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            flags |= PendingIntentFlags.Immutable;
        }

        return flags;
    }

    private static int GetNotificationId(string poiId)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in poiId)
            {
                hash = (hash * 31) + character;
            }

            return 7002 + ((hash & 0x7fffffff) % 1000000);
        }
    }
}
#endif
