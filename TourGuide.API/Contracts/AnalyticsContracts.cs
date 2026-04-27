namespace TourGuide.API.Contracts;

public sealed class PingRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
}

public sealed class QrScanRequest
{
    public string PoiId { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = "WebQR";
}

public sealed class QrScanResponse
{
    public bool Counted { get; set; }
    public bool InCooldown { get; set; }
    public DateTime CooldownEndsAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class NarrationPlayRequest
{
    public string PoiId { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = "WebQR";
}

public sealed class NarrationPlayResponse
{
    public string LogId { get; set; } = string.Empty;
    public bool Counted { get; set; }
    public bool RateLimited { get; set; }
}

public sealed class NarrationFinishRequest
{
    public string LogId { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public int DwellTimeSeconds { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
}

public sealed class OverviewMetric
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Tone { get; set; } = "default";
}

public sealed class TrendPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class HeatmapPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Intensity { get; set; }
}

public sealed class AuditFeedItem
{
    public string ActionType { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public sealed class AdminOverviewResponse
{
    public int ActiveUsers { get; set; }
    public int TotalOwners { get; set; }
    public int TotalPois { get; set; }
    public int PendingPois { get; set; }
    public int ApprovedPois { get; set; }
    public int CountedQrScans { get; set; }
    public int CountedTtsPlays { get; set; }
    public decimal MonthlyRecurringRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public IReadOnlyList<OverviewMetric> Metrics { get; set; } = Array.Empty<OverviewMetric>();
    public IReadOnlyList<TrendPoint> QrTrend { get; set; } = Array.Empty<TrendPoint>();
    public IReadOnlyList<TrendPoint> TtsTrend { get; set; } = Array.Empty<TrendPoint>();
    public IReadOnlyList<AuditFeedItem> RecentAuditLogs { get; set; } = Array.Empty<AuditFeedItem>();
}

public sealed class OwnerPoiPerformance
{
    public string PoiId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CountedQrScans { get; set; }
    public int CountedTtsPlays { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SubscriptionPackage { get; set; } = string.Empty;
}

public sealed class OwnerAlertResponse
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class OwnerOverviewResponse
{
    public string OwnerId { get; set; } = string.Empty;
    public int TotalPois { get; set; }
    public int ApprovedPois { get; set; }
    public int CountedQrScans { get; set; }
    public int CountedTtsPlays { get; set; }
    public int ReplayTtsPlays { get; set; }
    public decimal Revenue { get; set; }
    public decimal MonthlyRecurringRevenue { get; set; }
    public double ContentCompletenessScore { get; set; }
    public double RegionalAverageQrScans { get; set; }
    public double RegionalAverageTtsPlays { get; set; }
    public IReadOnlyList<OwnerPoiPerformance> TopPois { get; set; } = Array.Empty<OwnerPoiPerformance>();
    public IReadOnlyList<TrendPoint> QrTrend { get; set; } = Array.Empty<TrendPoint>();
    public IReadOnlyList<TrendPoint> TtsTrend { get; set; } = Array.Empty<TrendPoint>();
    public IReadOnlyList<OwnerAlertResponse> Alerts { get; set; } = Array.Empty<OwnerAlertResponse>();
}
