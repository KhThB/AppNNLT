using System.Collections.Concurrent;
using System.Threading;
using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Implementations;

public sealed class SystemMetricsCollector
{
    private const int MaxSamples = 1024;
    private readonly ConcurrentQueue<double> _latencySamples = new();
    private long _requestCount;
    private long _serverErrorCount;
    private DateTime? _lastErrorAt;

    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public SystemMetricsResponse Snapshot()
    {
        var samples = _latencySamples.ToArray();
        Array.Sort(samples);

        var p95 = samples.Length == 0
            ? 0
            : samples[Math.Clamp((int)Math.Ceiling(samples.Length * 0.95d) - 1, 0, samples.Length - 1)];

        return new SystemMetricsResponse
        {
            StartedAt = StartedAt,
            UptimeSeconds = Math.Round((DateTime.UtcNow - StartedAt).TotalSeconds, 2),
            RequestCount = Interlocked.Read(ref _requestCount),
            AverageLatencyMs = samples.Length == 0 ? 0 : Math.Round(samples.Average(), 2),
            P95LatencyMs = Math.Round(p95, 2),
            ServerErrorCount = Interlocked.Read(ref _serverErrorCount),
            LastErrorAt = _lastErrorAt,
        };
    }

    public void Record(double elapsedMs, int statusCode, bool exceptionThrown)
    {
        Interlocked.Increment(ref _requestCount);
        _latencySamples.Enqueue(Math.Round(elapsedMs, 2));
        while (_latencySamples.Count > MaxSamples && _latencySamples.TryDequeue(out _))
        {
        }

        if (exceptionThrown || statusCode >= StatusCodes.Status500InternalServerError)
        {
            Interlocked.Increment(ref _serverErrorCount);
            _lastErrorAt = DateTime.UtcNow;
        }
    }
}
