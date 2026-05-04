using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

public class TranslationCache
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string PoiId { get; set; } = string.Empty;

    public int ContentVersion { get; set; } = 1;
    public string SourceHash { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "vi";
    public string TargetLanguage { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string Status { get; set; } = TranslationStatuses.PendingManual;
    public string Provider { get; set; } = "ManualFallback";
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
