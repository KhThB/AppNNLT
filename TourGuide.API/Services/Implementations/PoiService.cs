using System.Security.Claims;
using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public sealed class PoiService : IPoiService
{
    private readonly MongoCollections _collections;
    private readonly IAuditService _auditService;
    private readonly ITranslationService _translationService;
    private readonly IModerationService _moderationService;

    public PoiService(
        MongoCollections collections,
        IAuditService auditService,
        ITranslationService translationService,
        IModerationService moderationService)
    {
        _collections = collections;
        _auditService = auditService;
        _translationService = translationService;
        _moderationService = moderationService;
    }

    public async Task<PoiListItemResponse> CreateAsync(string ownerId, PoiCreateRequest request, CancellationToken cancellationToken = default)
    {
        var poi = new POI
        {
            OwnerId = ownerId,
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            SourceLanguage = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "vi" : request.SourceLanguage.Trim(),
            SourceDescription = request.SourceDescription.Trim(),
            Description_VI = request.SourceDescription.Trim(),
            MerchantNote = request.MerchantNote.Trim(),
            Location = new GeoLocation
            {
                Type = "Point",
                Coordinates = [request.Longitude, request.Latitude],
            },
            Radius = request.Radius,
            SubscriptionPackage = request.SubscriptionPackage,
            Status = PoiWorkflowStatuses.Draft,
            ApprovalStatus = PoiWorkflowStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        poi.ModerationStatus = await _moderationService.EvaluateAsync(poi, cancellationToken);
        await _collections.Pois.InsertOneAsync(poi, cancellationToken: cancellationToken);
        await _translationService.RefreshAsync(poi, cancellationToken);
        await UpsertBillingRecordAsync(poi, cancellationToken);

        await _auditService.WriteAsync(
            "POI_CREATED",
            "POI",
            poi.Id,
            new { message = "POI draft created" },
            ownerId,
            KnownRoles.Merchant,
            cancellationToken);

        return Map(poi);
    }

    public async Task<PoiListItemResponse?> GetByIdAsync(string poiId, CancellationToken cancellationToken = default)
    {
        var poi = await _collections.Pois.Find(x => x.Id == poiId).FirstOrDefaultAsync(cancellationToken);
        return poi == null ? null : Map(poi);
    }

    public async Task<PoiPublicDetailResponse?> GetPublicDetailAsync(string poiId, CancellationToken cancellationToken = default)
    {
        var poi = await _collections.Pois.Find(x => x.Id == poiId && x.Status == PoiWorkflowStatuses.Approved)
            .FirstOrDefaultAsync(cancellationToken);
        if (poi == null)
        {
            return null;
        }

        var contents = await _translationService.ResolveForPublicAsync(poi, cancellationToken);
        return new PoiPublicDetailResponse
        {
            Id = poi.Id,
            Name = poi.Name,
            Address = poi.Address,
            ImageUrl = poi.ImageUrl,
            Latitude = poi.Location.Coordinates.ElementAtOrDefault(1),
            Longitude = poi.Location.Coordinates.ElementAtOrDefault(0),
            Contents = contents,
        };
    }

    public async Task<PagedResponse<PoiListItemResponse>> QueryAsync(PoiQueryRequest request, CancellationToken cancellationToken = default)
    {
        var filter = Builders<POI>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            filter &= Builders<POI>.Filter.Eq(x => x.Status, request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.OwnerId))
        {
            filter &= Builders<POI>.Filter.Eq(x => x.OwnerId, request.OwnerId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(request.Search.Trim(), "i");
            filter &= Builders<POI>.Filter.Or(
                Builders<POI>.Filter.Regex(x => x.Name, regex),
                Builders<POI>.Filter.Regex(x => x.Address, regex));
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var total = await _collections.Pois.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _collections.Pois.Find(filter)
            .SortByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<PoiListItemResponse>
        {
            Items = items.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
        };
    }

    public async Task<IReadOnlyList<PoiListItemResponse>> GetOwnerPoisAsync(string ownerId, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var resolvedOwnerId = BusinessRules.ResolveAccessibleOwnerId(
            ownerId,
            principal.FindFirstValue(ClaimTypes.Role),
            principal.FindFirstValue(ClaimTypes.NameIdentifier));

        var items = await _collections.Pois.Find(x => x.OwnerId == resolvedOwnerId)
            .SortByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        return items.OrderByDescending(BusinessRules.CalculatePriorityScore).Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PoiListItemResponse>> GetNearbyAsync(NearbyPoiQueryRequest request, CancellationToken cancellationToken = default)
    {
        var maxDistance = Math.Clamp(request.MaxDistance, 1, 100_000);
        var pois = await _collections.Pois.Find(x => x.Status == PoiWorkflowStatuses.Approved)
            .ToListAsync(cancellationToken);

        return pois
            .Where(x => BusinessRules.HaversineDistance(
                request.Latitude,
                request.Longitude,
                x.Location.Coordinates.ElementAtOrDefault(1),
                x.Location.Coordinates.ElementAtOrDefault(0)) <= maxDistance)
            .OrderByDescending(BusinessRules.CalculatePriorityScore)
            .ThenBy(x => BusinessRules.HaversineDistance(
                request.Latitude,
                request.Longitude,
                x.Location.Coordinates.ElementAtOrDefault(1),
                x.Location.Coordinates.ElementAtOrDefault(0)))
            .Take(100)
            .Select(Map)
            .ToList();
    }

    public async Task<PoiListItemResponse> UpdateAsync(string poiId, ClaimsPrincipal principal, PoiUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var poi = await RequireEditablePoiAsync(poiId, principal, cancellationToken);
        poi.Name = request.Name.Trim();
        poi.Address = request.Address.Trim();
        poi.SourceLanguage = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "vi" : request.SourceLanguage.Trim();
        poi.SourceDescription = request.SourceDescription.Trim();
        poi.Description_VI = request.SourceDescription.Trim();
        poi.MerchantNote = request.MerchantNote.Trim();
        poi.Location = new GeoLocation { Type = "Point", Coordinates = [request.Longitude, request.Latitude] };
        poi.Radius = request.Radius;
        poi.PriorityLevel = request.PriorityLevel;
        poi.BoostPriority = request.BoostPriority;
        poi.BoostExpiresAt = request.BoostPriority > 0
            ? DateTime.UtcNow.AddMonths(1)
            : null;
        poi.ImageUrl = request.ImageUrl;
        poi.SubscriptionPackage = request.SubscriptionPackage;
        poi.UpdatedAt = DateTime.UtcNow;
        poi.ContentVersion += 1;
        poi.TranslationStatus = TranslationStatuses.Pending;
        poi.ModerationStatus = await _moderationService.EvaluateAsync(poi, cancellationToken);

        if (principal.FindFirstValue(ClaimTypes.Role) == KnownRoles.Merchant)
        {
            poi.Status = PoiWorkflowStatuses.Draft;
            poi.ApprovalStatus = PoiWorkflowStatuses.Draft;
            poi.ReviewedBy = null;
            poi.ReviewedAt = null;
            poi.RejectionReason = string.Empty;
        }

        await _collections.Pois.ReplaceOneAsync(x => x.Id == poi.Id, poi, cancellationToken: cancellationToken);
        await _translationService.RefreshAsync(poi, cancellationToken);
        await UpsertBillingRecordAsync(poi, cancellationToken);

        await _auditService.WriteAsync(
            "POI_UPDATED",
            "POI",
            poi.Id,
            new { message = "POI updated", status = poi.Status },
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            principal.FindFirstValue(ClaimTypes.Role),
            cancellationToken);

        return Map(poi);
    }

    public async Task<PoiListItemResponse> SubmitAsync(string poiId, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var poi = await RequireEditablePoiAsync(poiId, principal, cancellationToken);
        poi.Status = PoiWorkflowStatuses.Submitted;
        poi.ApprovalStatus = PoiWorkflowStatuses.Submitted;
        poi.UpdatedAt = DateTime.UtcNow;
        await _collections.Pois.ReplaceOneAsync(x => x.Id == poi.Id, poi, cancellationToken: cancellationToken);

        await _auditService.WriteAsync(
            "POI_SUBMITTED",
            "POI",
            poi.Id,
            new { message = "POI submitted for review" },
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            principal.FindFirstValue(ClaimTypes.Role),
            cancellationToken);

        return Map(poi);
    }

    public async Task<PoiListItemResponse> ReviewAsync(string poiId, ClaimsPrincipal principal, PoiReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (principal.FindFirstValue(ClaimTypes.Role) != KnownRoles.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ admin mới được duyệt POI.");
        }

        var poi = await _collections.Pois.Find(x => x.Id == poiId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy POI.");

        BusinessRules.ApplyReview(
            poi,
            request.Approve,
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            request.RejectionReason.Trim(),
            DateTime.UtcNow);

        if (request.Approve && poi.ModerationStatus == ModerationStatuses.PendingManual)
        {
            poi.ModerationStatus = ModerationStatuses.Approved;
        }

        await _collections.Pois.ReplaceOneAsync(x => x.Id == poi.Id, poi, cancellationToken: cancellationToken);
        await CreateOwnerAlertAsync(
            poi.OwnerId,
            poi.Id,
            request.Approve ? "POI_APPROVED" : "POI_REJECTED",
            request.Approve ? "POI đã được duyệt" : "POI bị từ chối",
            request.Approve ? $"POI {poi.Name} đã được admin duyệt." : $"POI {poi.Name} bị từ chối: {request.RejectionReason}",
            request.Approve ? "Success" : "Warning",
            cancellationToken);

        await _auditService.WriteAsync(
            request.Approve ? "POI_APPROVED" : "POI_REJECTED",
            "POI",
            poi.Id,
            new { message = request.Approve ? "POI approved" : "POI rejected", request.RejectionReason },
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            principal.FindFirstValue(ClaimTypes.Role),
            cancellationToken);

        return Map(poi);
    }

    public async Task<string> UpdateImageAsync(string poiId, ClaimsPrincipal principal, string imageUrl, CancellationToken cancellationToken = default)
    {
        var poi = await RequireEditablePoiAsync(poiId, principal, cancellationToken);
        poi.ImageUrl = imageUrl;
        poi.UpdatedAt = DateTime.UtcNow;
        await _collections.Pois.ReplaceOneAsync(x => x.Id == poi.Id, poi, cancellationToken: cancellationToken);
        return imageUrl;
    }

    private async Task<POI> RequireEditablePoiAsync(string poiId, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var poi = await _collections.Pois.Find(x => x.Id == poiId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy POI.");

        var role = principal.FindFirstValue(ClaimTypes.Role);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (role != KnownRoles.Admin && poi.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền sửa POI này.");
        }

        return poi;
    }

    private async Task UpsertBillingRecordAsync(POI poi, CancellationToken cancellationToken)
    {
        if (poi.SubscriptionPackage == SubscriptionPackages.Basic && poi.BoostPriority <= 0)
        {
            return;
        }

        var amount = poi.SubscriptionPackage == SubscriptionPackages.Premium ? 150_000m : 50_000m;
        if (poi.BoostPriority > 0)
        {
            amount += poi.BoostPriority * 25_000m;
        }

        var billingType = poi.BoostPriority > 0 ? "Boost" : "Subscription";
        var filter = Builders<BillingRecord>.Filter.Eq(x => x.PoiId, poi.Id) &
                     Builders<BillingRecord>.Filter.Eq(x => x.BillingType, billingType);

        var existing = await _collections.BillingRecords.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            existing = new BillingRecord
            {
                OwnerId = poi.OwnerId,
                PoiId = poi.Id,
                BillingType = billingType,
                PackageName = poi.SubscriptionPackage,
                Amount = amount,
                Status = "Active",
                EffectiveFrom = DateTime.UtcNow,
                EffectiveTo = DateTime.UtcNow.AddMonths(1),
                AutoRenew = poi.SubscriptionPackage != SubscriptionPackages.Basic,
                Notes = "Stub billing record",
            };

            await _collections.BillingRecords.InsertOneAsync(existing, cancellationToken: cancellationToken);
            return;
        }

        existing.PackageName = poi.SubscriptionPackage;
        existing.Amount = amount;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Status = "Active";
        existing.EffectiveTo ??= DateTime.UtcNow.AddMonths(1);
        await _collections.BillingRecords.ReplaceOneAsync(x => x.Id == existing.Id, existing, cancellationToken: cancellationToken);
    }

    private async Task CreateOwnerAlertAsync(
        string ownerId,
        string poiId,
        string alertType,
        string title,
        string message,
        string severity,
        CancellationToken cancellationToken)
    {
        await _collections.OwnerAlerts.InsertOneAsync(
            new OwnerAlert
            {
                OwnerId = ownerId,
                PoiId = poiId,
                AlertType = alertType,
                Title = title,
                Message = message,
                Severity = severity,
                CreatedAt = DateTime.UtcNow,
            },
            cancellationToken: cancellationToken);
    }

    private static PoiListItemResponse Map(POI poi)
    {
        return new PoiListItemResponse
        {
            Id = poi.Id,
            OwnerId = poi.OwnerId,
            Name = poi.Name,
            Address = poi.Address,
            Status = poi.Status,
            ModerationStatus = poi.ModerationStatus,
            TranslationStatus = poi.TranslationStatus,
            SubscriptionPackage = poi.SubscriptionPackage,
            ImageUrl = poi.ImageUrl,
            Radius = poi.Radius,
            PriorityLevel = poi.PriorityLevel,
            BoostPriority = poi.BoostPriority,
            CountedQrScanCount = poi.CountedQrScanCount,
            CountedTtsPlayCount = poi.CountedTtsPlayCount,
            CreatedAt = poi.CreatedAt,
            UpdatedAt = poi.UpdatedAt,
            Latitude = poi.Location.Coordinates.ElementAtOrDefault(1),
            Longitude = poi.Location.Coordinates.ElementAtOrDefault(0),
            SourceDescription = poi.SourceDescription,
        };
    }
}
