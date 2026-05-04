using MongoDB.Driver;
using MongoDB.Bson;
using TourGuide.API.Services.Implementations;
using TourGuide.Domain.Models;

namespace TourGuide.API.Infrastructure.Mongo;

public sealed class MongoIndexInitializer
{
    private readonly MongoCollections _collections;

    public MongoIndexInitializer(MongoCollections collections)
    {
        _collections = collections;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await NormalizeLegacyGeoJsonAsync("POIs", cancellationToken);
        await NormalizeLegacyGeoJsonAsync("TrackingData", cancellationToken);
        await NormalizeLegacyPoiFieldsAsync(cancellationToken);
        await NormalizeLegacyUserIdentityAsync(cancellationToken);
        await BackfillPoiTagsAsync(cancellationToken);
        await NormalizePoiRadiusAsync(cancellationToken);
        await BackfillApprovedPoiSnapshotsAsync(cancellationToken);
        await RepairDuplicateQrCountedWindowsAsync(cancellationToken);

        await _collections.Pois.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<POI>(
                    Builders<POI>.IndexKeys.Geo2DSphere("Location")),
                new CreateIndexModel<POI>(
                    Builders<POI>.IndexKeys.Ascending(x => x.OwnerId).Ascending(x => x.Status)),
            },
            cancellationToken);

        await _collections.TrackingData.Indexes.CreateOneAsync(
            new CreateIndexModel<TrackingData>(Builders<TrackingData>.IndexKeys.Geo2DSphere("Location")),
            cancellationToken: cancellationToken);

        await CreateUserIndexesSafelyAsync(cancellationToken);

        await _collections.QrScanLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<QrScanLog>(
                Builders<QrScanLog>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Ascending(x => x.VisitorId)
                    .Ascending(x => x.WindowKey)
                    .Descending(x => x.OccurredAt)),
            cancellationToken: cancellationToken);

        await _collections.QrScanLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<QrScanLog>(
                Builders<QrScanLog>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Ascending(x => x.VisitorId)
                    .Ascending(x => x.WindowKey),
                new CreateIndexOptions<QrScanLog>
                {
                    Name = "ux_qr_counted_window",
                    Unique = true,
                    PartialFilterExpression = Builders<QrScanLog>.Filter.Eq(x => x.Counted, true),
                }),
            cancellationToken: cancellationToken);

        await _collections.NarrationLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<NarrationLog>(
                Builders<NarrationLog>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Ascending(x => x.VisitorId)
                    .Ascending(x => x.SessionId)
                    .Descending(x => x.OccurredAt)),
            cancellationToken: cancellationToken);

        await _collections.NarrationLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<NarrationLog>(
                Builders<NarrationLog>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Ascending(x => x.VisitorId)
                    .Ascending(x => x.SessionId)
                    .Ascending(x => x.WindowKey)
                    .Descending(x => x.StartedAt)),
            cancellationToken: cancellationToken);

        await _collections.TranslationCaches.Indexes.CreateOneAsync(
            new CreateIndexModel<TranslationCache>(
                Builders<TranslationCache>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Ascending(x => x.TargetLanguage)
                    .Ascending(x => x.ContentVersion)),
            cancellationToken: cancellationToken);

        await _collections.UserSessions.Indexes.CreateOneAsync(
            new CreateIndexModel<UserSession>(
                Builders<UserSession>.IndexKeys.Ascending(x => x.SessionId),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await _collections.PoiReviewSnapshots.Indexes.CreateOneAsync(
            new CreateIndexModel<PoiReviewSnapshot>(
                Builders<PoiReviewSnapshot>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Descending(x => x.ApprovedAt)),
            cancellationToken: cancellationToken);
    }

    private async Task NormalizeLegacyGeoJsonAsync(string collectionName, CancellationToken cancellationToken)
    {
        var collection = _collections.Database.GetCollection<BsonDocument>(collectionName);
        var filter = Builders<BsonDocument>.Filter.Exists("Location.Type");
        var updates = new[]
        {
            new BsonDocument("$set", new BsonDocument
            {
                { "Location.type", "$Location.Type" },
                { "Location.coordinates", "$Location.Coordinates" },
            }),
            new BsonDocument("$unset", new BsonArray { "Location.Type", "Location.Coordinates" }),
        };

        await collection.UpdateManyAsync(
            filter,
            new PipelineUpdateDefinition<BsonDocument>(updates),
            cancellationToken: cancellationToken);
    }

    private async Task NormalizeLegacyPoiFieldsAsync(CancellationToken cancellationToken)
    {
        var collection = _collections.Database.GetCollection<BsonDocument>("POIs");
        var boostExpireFilter = Builders<BsonDocument>.Filter.Exists("BoostExpireDate");
        var boostExpireUpdates = new[]
        {
            new BsonDocument("$set", new BsonDocument("BoostExpiresAt", "$BoostExpireDate")),
            new BsonDocument("$unset", "BoostExpireDate"),
        };

        await collection.UpdateManyAsync(
            boostExpireFilter,
            new PipelineUpdateDefinition<BsonDocument>(boostExpireUpdates),
            cancellationToken: cancellationToken);
    }

    private async Task BackfillPoiTagsAsync(CancellationToken cancellationToken)
    {
        var pois = await _collections.Pois.Find(FilterDefinition<POI>.Empty).ToListAsync(cancellationToken);
        foreach (var poi in pois.Where(x => BusinessRules.NormalizeTags(x.Tags).Count == 0))
        {
            await _collections.Pois.UpdateOneAsync(
                x => x.Id == poi.Id,
                Builders<POI>.Update.Set(x => x.Tags, BusinessRules.InferTags(poi.Name, poi.SourceDescription).ToList()),
                cancellationToken: cancellationToken);
        }
    }

    private async Task BackfillApprovedPoiSnapshotsAsync(CancellationToken cancellationToken)
    {
        var approvedPois = await _collections.Pois.Find(x => x.Status == PoiWorkflowStatuses.Approved)
            .ToListAsync(cancellationToken);
        if (approvedPois.Count == 0)
        {
            return;
        }

        var poiIds = approvedPois.Select(x => x.Id).ToList();
        var existingPoiIds = await _collections.PoiReviewSnapshots
            .Distinct<string>(nameof(PoiReviewSnapshot.PoiId), Builders<PoiReviewSnapshot>.Filter.In(x => x.PoiId, poiIds))
            .ToListAsync(cancellationToken);
        var existing = new HashSet<string>(existingPoiIds);

        foreach (var poi in approvedPois.Where(x => !existing.Contains(x.Id)))
        {
            var approvedAt = poi.ReviewedAt ?? poi.UpdatedAt;
            var snapshot = BusinessRules.CreateApprovedSnapshot(poi, poi.ReviewedBy, approvedAt);
            await _collections.PoiReviewSnapshots.InsertOneAsync(snapshot, cancellationToken: cancellationToken);
        }
    }

    private async Task NormalizePoiRadiusAsync(CancellationToken cancellationToken)
    {
        var pois = await _collections.Pois.Find(FilterDefinition<POI>.Empty).ToListAsync(cancellationToken);
        foreach (var poi in pois)
        {
            var effectiveRadius = BusinessRules.GetEffectiveRadius(poi);
            if (Math.Abs(poi.Radius - effectiveRadius) < 0.001)
            {
                continue;
            }

            await _collections.Pois.UpdateOneAsync(
                x => x.Id == poi.Id,
                Builders<POI>.Update.Set(x => x.Radius, effectiveRadius),
                cancellationToken: cancellationToken);
        }
    }

    private async Task RepairDuplicateQrCountedWindowsAsync(CancellationToken cancellationToken)
    {
        var counted = await _collections.QrScanLogs.Find(x => x.Counted)
            .SortBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        var duplicateIds = counted
            .GroupBy(x => new { x.PoiId, x.VisitorId, x.WindowKey })
            .SelectMany(group => group.Skip(1).Select(x => x.Id))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (duplicateIds.Count == 0)
        {
            return;
        }

        await _collections.QrScanLogs.UpdateManyAsync(
            x => duplicateIds.Contains(x.Id),
            Builders<QrScanLog>.Update.Set(x => x.Counted, false),
            cancellationToken: cancellationToken);
    }

    private async Task NormalizeLegacyUserIdentityAsync(CancellationToken cancellationToken)
    {
        var collection = _collections.Database.GetCollection<BsonDocument>("Users");
        var filter = Builders<BsonDocument>.Filter.Eq("AuthProvider", "Local") &
                     Builders<BsonDocument>.Filter.Or(
                         Builders<BsonDocument>.Filter.Exists("ProviderId", false),
                         Builders<BsonDocument>.Filter.Eq("ProviderId", string.Empty));
        var updates = new[]
        {
            new BsonDocument("$set", new BsonDocument("ProviderId", "$Phone")),
        };

        await collection.UpdateManyAsync(
            filter,
            new PipelineUpdateDefinition<BsonDocument>(updates),
            cancellationToken: cancellationToken);
    }

    private async Task CreateUserIndexesSafelyAsync(CancellationToken cancellationToken)
    {
        await DropIndexIfExistsAsync(_collections.Users, "Phone_1", cancellationToken);
        await DropIndexIfExistsAsync(_collections.Users, "AuthProvider_1_ProviderId_1", cancellationToken);

        var nonEmptyPhoneFilter = Builders<User>.Filter.And(
            Builders<User>.Filter.Exists(x => x.Phone),
            Builders<User>.Filter.Gt(x => x.Phone, string.Empty));
        var nonEmptyProviderIdFilter = Builders<User>.Filter.And(
            Builders<User>.Filter.Exists(x => x.ProviderId),
            Builders<User>.Filter.Gt(x => x.ProviderId, string.Empty));

        await CreateIndexWithFallbackAsync(
            _collections.Users,
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.Phone),
                new CreateIndexOptions<User> { Unique = true, PartialFilterExpression = nonEmptyPhoneFilter }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.Phone),
                new CreateIndexOptions<User> { PartialFilterExpression = nonEmptyPhoneFilter }),
            cancellationToken);

        await CreateIndexWithFallbackAsync(
            _collections.Users,
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.AuthProvider).Ascending(x => x.ProviderId),
                new CreateIndexOptions<User> { Unique = true, PartialFilterExpression = nonEmptyProviderIdFilter }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.AuthProvider).Ascending(x => x.ProviderId),
                new CreateIndexOptions<User> { PartialFilterExpression = nonEmptyProviderIdFilter }),
            cancellationToken);
    }

    private static async Task DropIndexIfExistsAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string indexName,
        CancellationToken cancellationToken)
    {
        try
        {
            await collection.Indexes.DropOneAsync(indexName, cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexNotFound")
        {
        }
    }

    private static async Task CreateIndexWithFallbackAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        CreateIndexModel<TDocument> preferredIndex,
        CreateIndexModel<TDocument> fallbackIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            await collection.Indexes.CreateOneAsync(preferredIndex, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException)
        {
            await collection.Indexes.CreateOneAsync(fallbackIndex, cancellationToken: cancellationToken);
        }
    }
}
