using MongoDB.Driver;
using TourGuide.Domain.Models;

namespace TourGuide.API.Infrastructure.Mongo;

public sealed class MongoCollections
{
    public MongoCollections(IMongoDatabase database)
    {
        Database = database;
    }

    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users => Database.GetCollection<User>("Users");
    public IMongoCollection<POI> Pois => Database.GetCollection<POI>("POIs");
    public IMongoCollection<NarrationLog> NarrationLogs => Database.GetCollection<NarrationLog>("NarrationLogs");
    public IMongoCollection<TrackingData> TrackingData => Database.GetCollection<TrackingData>("TrackingData");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("AuditLogs");
    public IMongoCollection<UserSession> UserSessions => Database.GetCollection<UserSession>("UserSessions");
    public IMongoCollection<QrScanLog> QrScanLogs => Database.GetCollection<QrScanLog>("QrScanLogs");
    public IMongoCollection<TranslationCache> TranslationCaches => Database.GetCollection<TranslationCache>("TranslationCaches");
    public IMongoCollection<BillingRecord> BillingRecords => Database.GetCollection<BillingRecord>("BillingRecords");
    public IMongoCollection<OwnerAlert> OwnerAlerts => Database.GetCollection<OwnerAlert>("OwnerAlerts");
}
