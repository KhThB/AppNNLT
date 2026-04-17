using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

public class POI
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = "";

    public string Name { get; set; } = "";

    // Multi-language descriptions
    public string Description_VI { get; set; } = "";
    public string Description_EN { get; set; } = "";
    public string Description_KO { get; set; } = "";
    public string Description_JA { get; set; } = "";
    public string Description_ZH { get; set; } = "";

    public GeoLocation Location { get; set; }

    public int PriorityLevel { get; set; } = 0;
    public DateTime? BoostExpireDate { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ImageUrl { get; set; } = "";
}

public class GeoLocation
{
    public string Type { get; set; } = "Point";
    public double[] Coordinates { get; set; }
}