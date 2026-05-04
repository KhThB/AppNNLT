using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

[BsonIgnoreExtraElements]
public class POI
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

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
    public string AudioUrl_VI { get; set; } = string.Empty;
    public string AudioUrl_EN { get; set; } = string.Empty;
    public string AudioUrl_KO { get; set; } = string.Empty;
    public string AudioUrl_JA { get; set; } = string.Empty;
    public string AudioUrl_ZH { get; set; } = string.Empty;
    public double Radius { get; set; } = 50;
    public GeoLocation Location { get; set; } = new();
    public int PriorityLevel { get; set; }
    public int BoostPriority { get; set; }
    public DateTime? BoostExpiresAt { get; set; }
    public string Status { get; set; } = PoiWorkflowStatuses.Draft;
    public string ApprovalStatus { get; set; } = PoiWorkflowStatuses.Draft;
    public string ModerationStatus { get; set; } = ModerationStatuses.PendingManual;
    public string TranslationStatus { get; set; } = TranslationStatuses.Pending;
    public int ContentVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int CountedQrScanCount { get; set; }
    public int ReplayQrScanCount { get; set; }
    public int CountedTtsPlayCount { get; set; }
    public int ReplayTtsPlayCount { get; set; }
    public decimal Revenue { get; set; }
    public string SubscriptionPackage { get; set; } = SubscriptionPackages.Basic;
    public bool IsPaid { get; set; }
    public string? LastTransactionId { get; set; }
    public DateTime? SubscriptionExpiry { get; set; }
    public string MerchantNote { get; set; } = string.Empty;
}

public class GeoLocation
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = Array.Empty<double>();
}
