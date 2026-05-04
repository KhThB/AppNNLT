using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

[BsonIgnoreExtraElements]
public sealed class PoiReviewSnapshot
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string PoiId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string SourceLanguage { get; set; } = "vi";
    public string SourceDescription { get; set; } = string.Empty;
    public string Description_VI { get; set; } = string.Empty;
    public string Description_EN { get; set; } = string.Empty;
    public string Description_KO { get; set; } = string.Empty;
    public string Description_JA { get; set; } = string.Empty;
    public string Description_ZH { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string SubscriptionPackage { get; set; } = SubscriptionPackages.Basic;
    public double Radius { get; set; } = 50;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ContentVersion { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? ApprovedBy { get; set; }

    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
