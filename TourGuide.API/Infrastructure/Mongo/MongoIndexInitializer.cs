using MongoDB.Driver;
using MongoDB.Bson;
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

        await _collections.NarrationLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<NarrationLog>(
                Builders<NarrationLog>.IndexKeys
                    .Ascending(x => x.PoiId)
                    .Ascending(x => x.VisitorId)
                    .Ascending(x => x.SessionId)
                    .Descending(x => x.OccurredAt)),
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
