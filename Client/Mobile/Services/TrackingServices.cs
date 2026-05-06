using Mobile.Models;

namespace Mobile.Services;

public interface ITrackingService
{
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
}

public sealed class TrackingService : ITrackingService
{
    private readonly GeofenceMonitorService _monitor;
#if !ANDROID
    private CancellationTokenSource? _foregroundCts;
#endif

    public TrackingService(GeofenceMonitorService monitor)
    {
        _monitor = monitor;
    }

    public bool IsRunning { get; private set; }

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        await EnsurePermissionsAsync();
        IsRunning = true;

#if ANDROID
        global::Mobile.Platforms.Android.TrackingForegroundService.Start();
#else
        _foregroundCts = new CancellationTokenSource();
        _ = Task.Run(() => _monitor.RunAsync(_ => Task.CompletedTask, _foregroundCts.Token));
#endif
    }

    public Task StopAsync()
    {
        if (!IsRunning)
        {
            return Task.CompletedTask;
        }

        IsRunning = false;
#if ANDROID
        global::Mobile.Platforms.Android.TrackingForegroundService.Stop();
#else
        _foregroundCts?.Cancel();
        _foregroundCts?.Dispose();
        _foregroundCts = null;
#endif
        return Task.CompletedTask;
    }

    private static async Task EnsurePermissionsAsync()
    {
        var location = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (location != PermissionStatus.Granted)
        {
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (always != PermissionStatus.Granted)
        {
            await Permissions.RequestAsync<Permissions.LocationAlways>();
        }

#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var notifications = await Permissions.CheckStatusAsync<PostNotificationsPermission>();
            if (notifications != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<PostNotificationsPermission>();
            }
        }
#endif
    }

#if ANDROID
    private sealed class PostNotificationsPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            OperatingSystem.IsAndroidVersionAtLeast(33)
                ? new[] { (Android.Manifest.Permission.PostNotifications, true) }
                : Array.Empty<(string androidPermission, bool isRuntime)>();
    }
#endif
}

public sealed class GeofenceMonitorService
{
    private static readonly TimeSpan MovingDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StationaryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PoiRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(30);

    private readonly PoiService _poiService;
    private List<PoiModel> _pois = new();
    private DateTime _lastPoiRefresh;
    private Location? _lastLocation;

    public GeofenceMonitorService(PoiService poiService)
    {
        _poiService = poiService;
    }

    public async Task RunAsync(Func<PoiModel, Task> onPoiEntered, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = MovingDelay;
            try
            {
                delay = await RunOnceAsync(onPoiEntered, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Geofence loop failed: {ex.Message}");
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    public async Task<TimeSpan> RunOnceAsync(Func<PoiModel, Task> onPoiEntered, CancellationToken cancellationToken = default)
    {
        await RefreshPoisIfNeededAsync(cancellationToken);

        var location = await Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)),
            cancellationToken);

        if (location == null)
        {
            return StationaryDelay;
        }

        await _poiService.SendPingAsync(location);

        var movedMeters = _lastLocation == null
            ? double.MaxValue
            : Location.CalculateDistance(
                _lastLocation.Latitude,
                _lastLocation.Longitude,
                location.Latitude,
                location.Longitude,
                DistanceUnits.Kilometers) * 1000;

        _lastLocation = location;

        var enteredPoi = _pois
            .Where(poi => poi.Latitude != 0 || poi.Longitude != 0)
            .Select(poi => new
            {
                Poi = poi,
                Distance = Location.CalculateDistance(location.Latitude, location.Longitude, poi.Latitude, poi.Longitude, DistanceUnits.Kilometers) * 1000,
                Radius = Math.Max(1, poi.Radius ?? 50),
            })
            .Where(x => x.Distance <= x.Radius)
            .OrderBy(x => x.Distance)
            .Select(x => x.Poi)
            .FirstOrDefault();

        if (enteredPoi != null && ShouldNotify(enteredPoi.Id))
        {
            MarkNotified(enteredPoi.Id);
            await onPoiEntered(enteredPoi);
        }

        return movedMeters < 10 ? StationaryDelay : MovingDelay;
    }

    private async Task RefreshPoisIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_pois.Count > 0 && DateTime.UtcNow - _lastPoiRefresh < PoiRefreshInterval)
        {
            return;
        }

        _pois = await _poiService.GetPoisAsync();
        _lastPoiRefresh = DateTime.UtcNow;
    }

    private static bool ShouldNotify(string? poiId)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return false;
        }

        var key = $"TourGuidePoiNotify:{poiId}";
        var lastTicks = Preferences.Default.Get(key, 0L);
        if (lastTicks <= 0)
        {
            return true;
        }

        var last = new DateTime(lastTicks, DateTimeKind.Utc);
        return DateTime.UtcNow - last >= NotificationCooldown;
    }

    private static void MarkNotified(string? poiId)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return;
        }

        Preferences.Default.Set($"TourGuidePoiNotify:{poiId}", DateTime.UtcNow.Ticks);
    }
}
