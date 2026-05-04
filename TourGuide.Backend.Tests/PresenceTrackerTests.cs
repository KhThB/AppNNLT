using TourGuide.API.Contracts;
using TourGuide.API.Services.Implementations;
using Xunit;

namespace TourGuide.Backend.Tests;

public sealed class PresenceTrackerTests
{
    [Fact]
    public void GetActive_ReturnsOnlyDevicesInsidePresenceWindow()
    {
        var tracker = new PresenceTracker();
        var now = DateTime.UtcNow;

        tracker.MarkSeen(new PingRequest
        {
            DeviceId = "device-online",
            SessionId = "session-online",
            Platform = "Android",
            Latitude = 10.7628,
            Longitude = 106.7005,
        }, now.AddSeconds(-5));

        tracker.MarkSeen(new PingRequest
        {
            DeviceId = "device-expired",
            SessionId = "session-expired",
            Platform = "Android",
        }, now.AddSeconds(-40));

        var active = tracker.GetActive(TimeSpan.FromSeconds(30));

        Assert.Single(active);
        Assert.Equal("device-online", active[0].DeviceId);
        Assert.Equal(10.7628, active[0].Latitude);
        Assert.Equal(106.7005, active[0].Longitude);
    }
}
