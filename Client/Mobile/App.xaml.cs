using Microsoft.Maui.Dispatching;
using Mobile.Services;

namespace Mobile;

public partial class App : Application
{
    private readonly PoiService _poiService;
    private readonly IDispatcherTimer? _pingTimer;
    private bool _isSendingPing;

    public App(PoiService poiService)
    {
        InitializeComponent();
        _poiService = poiService;

        _pingTimer = Dispatcher.CreateTimer();
        _pingTimer.Interval = TimeSpan.FromSeconds(10);
        _pingTimer.Tick += async (_, _) => await SendPingAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();
        await SendPingAsync();
        _pingTimer?.Start();
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _pingTimer?.Stop();
    }

    protected override async void OnResume()
    {
        base.OnResume();
        await SendPingAsync();
        _pingTimer?.Start();
    }

    private async Task SendPingAsync()
    {
        if (_isSendingPing)
        {
            return;
        }

        try
        {
            _isSendingPing = true;
            await _poiService.SendPingAsync();
        }
        finally
        {
            _isSendingPing = false;
        }
    }
}
