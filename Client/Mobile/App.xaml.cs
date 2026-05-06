using Mobile.Services;
using Mobile.Views;
using Microsoft.Maui.Storage;

namespace Mobile;

public partial class App : Application
{
    public const string PendingPoiNavigationKey = "poiId";

    private static bool _isNavigatingPendingPoi;
    private readonly ITrackingService _trackingService;

    public App(ITrackingService trackingService)
    {
        InitializeComponent();
        _trackingService = trackingService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();
        await StartTrackingAsync();
        await TryNavigateToPendingPoiAsync();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
    }

    protected override async void OnResume()
    {
        base.OnResume();
        await StartTrackingAsync();
        await TryNavigateToPendingPoiAsync();
    }

    public static async Task TryNavigateToPendingPoiAsync()
    {
        if (_isNavigatingPendingPoi)
        {
            return;
        }

        var poiId = Preferences.Default.Get(PendingPoiNavigationKey, string.Empty);
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return;
        }

        try
        {
            _isNavigatingPendingPoi = true;

            if (Shell.Current == null)
            {
                return;
            }

            Preferences.Default.Remove(PendingPoiNavigationKey);
            var route = $"{nameof(PoiDetailPage)}?poiId={Uri.EscapeDataString(poiId)}";
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open pending POI notification: {ex.Message}");
        }
        finally
        {
            _isNavigatingPendingPoi = false;
        }
    }

    private async Task StartTrackingAsync()
    {
        try
        {
            await _trackingService.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start tracking service: {ex.Message}");
        }
    }
}
