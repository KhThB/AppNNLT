using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

public class TrackingData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    public GeoLocation Location { get; set; }
    
    public double Speed { get; set; } // km/h, dùng để lọc nhiễu
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
