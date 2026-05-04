using System.Collections.Concurrent;
using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Implementations;

public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, OnlineDeviceResponse> _activeDevices = new();

    public void MarkSeen(PingRequest request, DateTime seenAtUtc)
    {
        var key = ResolvePresenceKey(request);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _activeDevices[key] = new OnlineDeviceResponse
        {
            DeviceId = request.DeviceId,
            UserId = request.UserId,
            SessionId = request.SessionId,
            Platform = request.Platform,
            AppVersion = request.AppVersion,
            DeviceName = request.DeviceName,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Speed = request.Speed,
            LastSeenAt = seenAtUtc,
        };
    }

    public int CountActive(TimeSpan threshold)
    {
        return GetActive(threshold).Count;
    }

    public IReadOnlyList<OnlineDeviceResponse> GetActive(TimeSpan threshold)
    {
        var now = DateTime.UtcNow;
        foreach (var item in _activeDevices)
        {
            if (now - item.Value.LastSeenAt > threshold)
            {
                _activeDevices.TryRemove(item.Key, out _);
            }
        }

        return _activeDevices.Values
            .Where(device => now - device.LastSeenAt <= threshold)
            .OrderByDescending(device => device.LastSeenAt)
            .ToList();
    }

    private static string ResolvePresenceKey(PingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            return request.UserId;
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return request.DeviceId;
        }

        return request.SessionId;
    }
}
