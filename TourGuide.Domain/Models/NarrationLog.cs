using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

public class NarrationLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string PoiId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = string.Empty;

    public string TriggerSource { get; set; } = "WebQR";
    public string WindowKey { get; set; } = string.Empty;
    public bool Counted { get; set; }
    public string ListenStatus { get; set; } = NarrationStatuses.Started;
    public int DwellTime { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
