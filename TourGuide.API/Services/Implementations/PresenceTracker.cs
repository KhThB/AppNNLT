using System.Collections.Concurrent;

namespace TourGuide.API.Services.Implementations;

public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _activeUsers = new();

    public void MarkSeen(string key, DateTime seenAtUtc)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _activeUsers[key] = seenAtUtc;
    }

    public int CountActive(TimeSpan threshold)
    {
        var now = DateTime.UtcNow;
        return _activeUsers.Values.Count(lastSeen => now - lastSeen <= threshold);
    }
}
