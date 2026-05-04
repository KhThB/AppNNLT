using MongoDB.Driver;
using Microsoft.AspNetCore.Http;
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
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AnalyticsService(
        MongoCollections collections,
        PresenceTracker presenceTracker,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _collections = collections;
        _presenceTracker = presenceTracker;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RecordPingAsync(PingRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _presenceTracker.MarkSeen(request, now);

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
            Timestamp = now,
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
        return _presenceTracker.CountActive(TimeSpan.FromSeconds(30));
    }

    public IReadOnlyList<OnlineDeviceResponse> GetOnlineDevices()
    {
        return _presenceTracker.GetActive(TimeSpan.FromSeconds(30));
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
            IPAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty,
            CooldownEndsAt = cooldownEndsAt,
            OccurredAt = now,
            CreatedAt = now,
        };

        await InsertQrScanLogAsync(log, cancellationToken);
        counted = log.Counted;
        cooldownEndsAt = log.CooldownEndsAt;

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
            Language = NormalizeLanguage(request.Language),
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
        var countedQrScans = (int)await _collections.QrScanLogs.CountDocumentsAsync(x => x.Counted, cancellationToken: cancellationToken);
        var countedTtsPlays = (int)await _collections.NarrationLogs.CountDocumentsAsync(x => x.Counted, cancellationToken: cancellationToken);

        var metrics = new List<OverviewMetric>
        {
            new() { Label = "Tài khoản chủ quán", Value = owners.ToString(), Tone = "primary" },
            new() { Label = "Tổng POI", Value = totalPois.ToString(), Tone = "primary" },
            new() { Label = "POI chờ duyệt", Value = pendingPois.ToString(), Tone = "warning" },
            new() { Label = "QR counted", Value = countedQrScans.ToString(), Tone = "success" },
            new() { Label = "TTS counted", Value = countedTtsPlays.ToString(), Tone = "info" },
        };

        return new AdminOverviewResponse
        {
            ActiveUsers = GetActiveUserCount(),
            TotalOwners = (int)owners,
            TotalPois = (int)totalPois,
            PendingPois = (int)pendingPois,
            ApprovedPois = (int)approvedPois,
            CountedQrScans = countedQrScans,
            CountedTtsPlays = countedTtsPlays,
            TotalRevenue = billing.Where(x => x.Status == BillingStatuses.Active).Sum(x => x.Amount),
            MonthlyRecurringRevenue = billing.Where(x => x.Status == BillingStatuses.Active).Sum(x => x.Amount),
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
        var countedQrScans = (int)await _collections.QrScanLogs.CountDocumentsAsync(x => x.OwnerId == ownerId && x.Counted, cancellationToken: cancellationToken);
        var countedTtsPlays = (int)await _collections.NarrationLogs.CountDocumentsAsync(x => x.OwnerId == ownerId && x.Counted, cancellationToken: cancellationToken);
        var replayTtsPlays = (int)await _collections.NarrationLogs.CountDocumentsAsync(x => x.OwnerId == ownerId && !x.Counted, cancellationToken: cancellationToken);
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
            CountedQrScans = countedQrScans,
            CountedTtsPlays = countedTtsPlays,
            ReplayTtsPlays = replayTtsPlays,
            Revenue = billing.Where(x => x.Status == BillingStatuses.Active).Sum(x => x.Amount),
            MonthlyRecurringRevenue = billing.Where(x => x.Status == BillingStatuses.Active).Sum(x => x.Amount),
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

    public async Task<RepairResponse> RepairAnalyticsCountersAsync(CancellationToken cancellationToken = default)
    {
        var pois = await _collections.Pois.Find(FilterDefinition<POI>.Empty).ToListAsync(cancellationToken);
        var qrLogs = await _collections.QrScanLogs.Find(FilterDefinition<QrScanLog>.Empty).ToListAsync(cancellationToken);
        var narrationLogs = await _collections.NarrationLogs.Find(FilterDefinition<NarrationLog>.Empty).ToListAsync(cancellationToken);
        var updated = 0;

        foreach (var poi in pois)
        {
            var countedQr = qrLogs.Count(x => x.PoiId == poi.Id && x.Counted);
            var replayQr = qrLogs.Count(x => x.PoiId == poi.Id && !x.Counted);
            var countedTts = narrationLogs.Count(x => x.PoiId == poi.Id && x.Counted);
            var replayTts = narrationLogs.Count(x => x.PoiId == poi.Id && !x.Counted);

            if (poi.CountedQrScanCount == countedQr &&
                poi.ReplayQrScanCount == replayQr &&
                poi.CountedTtsPlayCount == countedTts &&
                poi.ReplayTtsPlayCount == replayTts)
            {
                continue;
            }

            await _collections.Pois.UpdateOneAsync(
                x => x.Id == poi.Id,
                Builders<POI>.Update
                    .Set(x => x.CountedQrScanCount, countedQr)
                    .Set(x => x.ReplayQrScanCount, replayQr)
                    .Set(x => x.CountedTtsPlayCount, countedTts)
                    .Set(x => x.ReplayTtsPlayCount, replayTts)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
            updated++;
        }

        return new RepairResponse
        {
            Matched = pois.Count,
            Updated = updated,
            Message = "Analytics counters repaired from event logs.",
        };
    }

    private async Task InsertQrScanLogAsync(QrScanLog log, CancellationToken cancellationToken)
    {
        try
        {
            await _collections.QrScanLogs.InsertOneAsync(log, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (log.Counted && ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var lastCounted = await _collections.QrScanLogs
                .Find(x => x.PoiId == log.PoiId && x.VisitorId == log.VisitorId && x.WindowKey == log.WindowKey && x.Counted)
                .SortByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            log.Id = string.Empty;
            log.Counted = false;
            log.CooldownEndsAt = lastCounted?.CooldownEndsAt ?? BusinessRules.GetQrCooldownEnd(DateTime.UtcNow);
            await _collections.QrScanLogs.InsertOneAsync(log, cancellationToken: cancellationToken);
        }
    }

    private static string NormalizeLanguage(string language)
    {
        return (language ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "EN" => "EN",
            "KO" or "KR" => "KO",
            "JA" or "JP" => "JA",
            "ZH" or "CN" => "ZH",
            _ => "VI",
        };
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
