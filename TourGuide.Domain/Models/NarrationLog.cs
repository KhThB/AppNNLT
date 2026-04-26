using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

public class NarrationLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string PoiId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? OwnerId { get; set; } // Để dễ query doanh thu B2B

    public string TriggerType { get; set; } = "Geofence"; // Geofence / QRCode / Manual
    
    public string ListenStatus { get; set; } = "Completed"; // Completed / Skipped / Partial

    public int DwellTime { get; set; } = 0; // Thời gian nán lại (giây)

    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
