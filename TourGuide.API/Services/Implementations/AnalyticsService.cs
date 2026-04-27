using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly MongoCollections _collections;
    private readonly PresenceTracker _presenceTracker;
    private readonly IAuditService _auditService;

    public AnalyticsService(MongoCollections collections, PresenceTracker presenceTracker, IAuditService auditService)
    {
        _collections = collections;
        _presenceTracker = presenceTracker;
        _auditService = auditService;
    }

    public async Task RecordPingAsync(PingRequest request, CancellationToken cancellationToken = default)
    {
        var presenceKey = string.IsNullOrWhiteSpace(request.UserId) ? request.DeviceId : request.UserId;
        _presenceTracker.MarkSeen(presenceKey, DateTime.UtcNow);

        if (request.Latitude == 0 && request.Longitude == 0)
        {
            return;
        }

        var tracking = new TrackingData
        {
            UserId = request.UserId,
            DeviceId = request.DeviceId,
            SessionId = request.SessionId,
            Speed = request.Speed,
            Timestamp = DateTime.UtcNow,
            Location = new GeoLocation
            {
                Type = "Point",
                Coordinates = [request.Longitude, request.Latitude],
            },
        };

        await _collections.TrackingData.InsertOneAsync(tracking, cancellationToken: cancellationToken);
    }

    public int GetActiveUserCount()
    {
        return _presenceTracker.CountActive(TimeSpan.FromSeconds(15));
    }

    public async Task<QrScanResponse> RecordQrScanAsync(QrScanRequest request, CancellationToken cancellationToken = default)
    {
        var poi = await _collections.Pois.Find(x => x.Id == request.PoiId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy POI.");

        var now = DateTime.UtcNow;
        var lastCounted = await _collections.QrScanLogs.Find(x => x.PoiId == request.PoiId && x.VisitorId == request.VisitorId && x.Counted)
            .SortByDescending(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        var counted = BusinessRules.ShouldCountQrScan(lastCounted?.OccurredAt, now);
        var cooldownEndsAt = counted ? BusinessRules.GetQrCooldownEnd(now) : lastCounted?.CooldownEndsAt ?? BusinessRules.GetQrCooldownEnd(now);

        var log = new QrScanLog
        {
            PoiId = request.PoiId,
            OwnerId = poi.OwnerId,
            VisitorId = request.VisitorId,
            SessionId = request.SessionId,
            WindowKey = BusinessRules.BuildQrWindowKey(now),
            Counted = counted,
            TriggerSource = request.TriggerSource,
            CooldownEndsAt = cooldownEndsAt,
            OccurredAt = now,
            CreatedAt = now,
        };

        await _collections.QrScanLogs.InsertOneAsync(log, cancellationToken: cancellationToken);

        var update = counted
            ? Builders<POI>.Update.Inc(x => x.CountedQrScanCount, 1)
            : Builders<POI>.Update.Inc(x => x.ReplayQrScanCount, 1);

        await _collections.Pois.UpdateOneAsync(x => x.Id == request.PoiId, update, cancellationToken: cancellationToken);

        await _auditService.WriteAsync(
            counted ? "QR_SCAN_COUNTED" : "QR_SCAN_REPLAY",
            "POI",
            request.PoiId,
            new { message = counted ? "Counted QR scan" : "Replay QR scan", request.VisitorId, cooldownEndsAt },
            cancellationToken: cancellationToken);

        return new QrScanResponse
        {
            Counted = counted,
            InCooldown = !counted,
            CooldownEndsAt = cooldownEndsAt,
            Message = counted
                ? "Đã ghi nhận lượt quét QR."
                : $"Bạn đã quét mã này rồi. Có thể nghe lại, nhưng hệ thống sẽ không cộng thêm lượt trước {cooldownEndsAt:HH:mm dd/MM}.",
        };
    }

    public async Task<NarrationPlayResponse> StartNarrationAsync(NarrationPlayRequest request, CancellationToken cancellationToken = default)
    {
        var poi = await _collections.Pois.Find(x => x.Id == request.PoiId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy POI.");

        var threshold = DateTime.UtcNow.AddMinutes(-1);
        var recentCount = await _collections.NarrationLogs.CountDocumentsAsync(
            x => x.PoiId == request.PoiId &&
                 x.VisitorId == request.VisitorId &&
                 x.SessionId == request.SessionId &&
                 x.Counted &&
                 x.StartedAt >= threshold,
            cancellationToken: cancellationToken);

        var counted = BusinessRules.ShouldCountNarration((int)recentCount);
        var log = new NarrationLog
        {
            PoiId = request.PoiId,
            OwnerId = poi.OwnerId,
            VisitorId = request.VisitorId,
            SessionId = request.SessionId,
            TriggerSource = request.TriggerSource,
            WindowKey = BusinessRules.BuildMinuteWindowKey(DateTime.UtcNow),
            Counted = counted,
            ListenStatus = counted ? NarrationStatuses.Started : NarrationStatuses.Replay,
            StartedAt = DateTime.UtcNow,
            OccurredAt = DateTime.UtcNow,
        };

        await _collections.NarrationLogs.InsertOneAsync(log, cancellationToken: cancellationToken);
        var update = counted
            ? Builders<POI>.Update.Inc(x => x.CountedTtsPlayCount, 1)
            : Builders<POI>.Update.Inc(x => x.ReplayTtsPlayCount, 1);

        await _collections.Pois.UpdateOneAsync(x => x.Id == request.PoiId, update, cancellationToken: cancellationToken);

        return new NarrationPlayResponse
        {
            LogId = log.Id,
            Counted = counted,
            RateLimited = !counted,
        };
    }

    public async Task FinishNarrationAsync(NarrationFinishRequest request, CancellationToken cancellationToken = default)
    {
        var update = Builders<NarrationLog>.Update
            .Set(x => x.EndedAt, DateTime.UtcNow)
            .Set(x => x.DwellTime, request.DwellTimeSeconds)
            .Set(x => x.ListenStatus, request.Status)
            .Set(x => x.ErrorCode, request.ErrorCode);

        await _collections.NarrationLogs.UpdateOneAsync(x => x.Id == request.LogId, update, cancellationToken: cancellationToken);
    }

    public async Task<AdminOverviewResponse> GetAdminOverviewAsync(CancellationToken cancellationToken = default)
    {
        var owners = await _collections.Users.CountDocumentsAsync(x => x.Role == KnownRoles.Merchant, cancellationToken: cancellationToken);
        var totalPois = await _collections.Pois.CountDocumentsAsync(FilterDefinition<POI>.Empty, cancellationToken: cancellationToken);
        var pendingPois = await _collections.Pois.CountDocumentsAsync(x => x.Status == PoiWorkflowStatuses.Submitted, cancellationToken: cancellationToken);
        var approvedPois = await _collections.Pois.CountDocumentsAsync(x => x.Status == PoiWorkflowStatuses.Approved, cancellationToken: cancellationToken);

        var pois = await _collections.Pois.Find(FilterDefinition<POI>.Empty).ToListAsync(cancellationToken);
        var billing = await _collections.BillingRecords.Find(FilterDefinition<BillingRecord>.Empty).ToListAsync(cancellationToken);

        var metrics = new List<OverviewMetric>
        {
            new() { Label = "Owners", Value = owners.ToString(), Tone = "primary" },
            new() { Label = "POI chờ duyệt", Value = pendingPois.ToString(), Tone = "warning" },
            new() { Label = "QR counted", Value = pois.Sum(x => x.CountedQrScanCount).ToString(), Tone = "success" },
            new() { Label = "TTS counted", Value = pois.Sum(x => x.CountedTtsPlayCount).ToString(), Tone = "info" },
        };

        return new AdminOverviewResponse
        {
            ActiveUsers = GetActiveUserCount(),
            TotalOwners = (int)owners,
            TotalPois = (int)totalPois,
            PendingPois = (int)pendingPois,
            ApprovedPois = (int)approvedPois,
            CountedQrScans = pois.Sum(x => x.CountedQrScanCount),
            CountedTtsPlays = pois.Sum(x => x.CountedTtsPlayCount),
            TotalRevenue = billing.Sum(x => x.Amount),
            MonthlyRecurringRevenue = billing.Where(x => x.Status == "Active").Sum(x => x.Amount),
            Metrics = metrics,
            QrTrend = await BuildQrTrendAsync(null, cancellationToken),
            TtsTrend = await BuildTtsTrendAsync(null, cancellationToken),
            RecentAuditLogs = await _auditService.GetRecentAsync(12, cancellationToken),
        };
    }

    public async Task<OwnerOverviewResponse> GetOwnerOverviewAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var pois = await _collections.Pois.Find(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);
        var billing = await _collections.BillingRecords.Find(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);
        var alerts = await _collections.OwnerAlerts.Find(x => x.OwnerId == ownerId)
            .SortByDescending(x => x.CreatedAt)
            .Limit(8)
            .ToListAsync(cancellationToken);
        var allApprovedPois = await _collections.Pois.Find(x => x.Status == PoiWorkflowStatuses.Approved).ToListAsync(cancellationToken);

        var contentCompleteness = pois.Count == 0
            ? 0
            : pois.Average(x =>
                (string.IsNullOrWhiteSpace(x.SourceDescription) ? 0 : 50) +
                (string.IsNullOrWhiteSpace(x.ImageUrl) ? 0 : 25) +
                (string.IsNullOrWhiteSpace(x.Address) ? 0 : 25));

        return new OwnerOverviewResponse
        {
            OwnerId = ownerId,
            TotalPois = pois.Count,
            ApprovedPois = pois.Count(x => x.Status == PoiWorkflowStatuses.Approved),
            CountedQrScans = pois.Sum(x => x.CountedQrScanCount),
            CountedTtsPlays = pois.Sum(x => x.CountedTtsPlayCount),
            ReplayTtsPlays = pois.Sum(x => x.ReplayTtsPlayCount),
            Revenue = billing.Sum(x => x.Amount),
            MonthlyRecurringRevenue = billing.Where(x => x.Status == "Active").Sum(x => x.Amount),
            ContentCompletenessScore = Math.Round(contentCompleteness, 2),
            RegionalAverageQrScans = allApprovedPois.Count == 0 ? 0 : allApprovedPois.Average(x => x.CountedQrScanCount),
            RegionalAverageTtsPlays = allApprovedPois.Count == 0 ? 0 : allApprovedPois.Average(x => x.CountedTtsPlayCount),
            TopPois = pois
                .OrderByDescending(x => x.CountedQrScanCount)
                .Take(5)
                .Select(x => new OwnerPoiPerformance
                {
                    PoiId = x.Id,
                    Name = x.Name,
                    CountedQrScans = x.CountedQrScanCount,
                    CountedTtsPlays = x.CountedTtsPlayCount,
                    Status = x.Status,
                    SubscriptionPackage = x.SubscriptionPackage,
                })
                .ToList(),
            QrTrend = await BuildQrTrendAsync(ownerId, cancellationToken),
            TtsTrend = await BuildTtsTrendAsync(ownerId, cancellationToken),
            Alerts = alerts.Select(x => new OwnerAlertResponse
            {
                Title = x.Title,
                Message = x.Message,
                Severity = x.Severity,
                CreatedAt = x.CreatedAt,
            }).ToList(),
        };
    }

    public async Task<IReadOnlyList<HeatmapPoint>> GetHeatmapAsync(int hours, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddHours(-Math.Clamp(hours, 1, 48));
        var points = await _collections.TrackingData.Find(x => x.Timestamp >= since).ToListAsync(cancellationToken);

        return points
            .Where(x => x.Location.Coordinates.Length == 2)
            .GroupBy(x => new
            {
                Lat = Math.Round(x.Location.Coordinates[1], 3),
                Lng = Math.Round(x.Location.Coordinates[0], 3),
            })
            .Select(group => new HeatmapPoint
            {
                Latitude = group.Key.Lat,
                Longitude = group.Key.Lng,
                Intensity = group.Count(),
            })
            .OrderByDescending(x => x.Intensity)
            .Take(300)
            .ToList();
    }

    private async Task<IReadOnlyList<TrendPoint>> BuildQrTrendAsync(string? ownerId, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Date.AddDays(-6);
        var logs = await _collections.QrScanLogs.Find(x => x.Counted && x.OccurredAt >= since).ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            var ownerPoiIds = await _collections.Pois.Find(x => x.OwnerId == ownerId).Project(x => x.Id).ToListAsync(cancellationToken);
            logs = logs.Where(x => ownerPoiIds.Contains(x.PoiId)).ToList();
        }

        return Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = since.AddDays(offset);
                return new TrendPoint
                {
                    Label = day.ToString("dd/MM"),
                    Value = logs.Count(x => x.OccurredAt.Date == day.Date),
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TrendPoint>> BuildTtsTrendAsync(string? ownerId, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Date.AddDays(-6);
        var logs = await _collections.NarrationLogs.Find(x => x.Counted && x.StartedAt >= since).ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            logs = logs.Where(x => x.OwnerId == ownerId).ToList();
        }

        return Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = since.AddDays(offset);
                return new TrendPoint
                {
                    Label = day.ToString("dd/MM"),
                    Value = logs.Count(x => x.StartedAt.Date == day.Date),
                };
            })
            .ToList();
    }
}
