using System.Text.Json.Serialization;

namespace Mobile.Models;

public class PoiModel
{
    private GeoLocation? _location;

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("ownerId")]
    public string? OwnerId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonIgnore]
    public string TagSummary => Tags.Count == 0
        ? "Ăn vặt"
        : string.Join(", ", Tags.Select(ToDisplayTag));

    [JsonPropertyName("sourceLanguage")]
    public string? SourceLanguage { get; set; }

    [JsonPropertyName("sourceDescription")]
    public string? SourceDescription { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location
    {
        get => _location;
        set
        {
            _location = value;
            if (value?.Coordinates.Length >= 2)
            {
                Longitude = value.Coordinates[0];
                Latitude = value.Coordinates[1];
            }
        }
    }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("description_VI")]
    public string? Description_VI { get; set; }

    [JsonPropertyName("description_EN")]
    public string? Description_EN { get; set; }

    [JsonPropertyName("description_KO")]
    public string? Description_KO { get; set; }

    [JsonPropertyName("description_JA")]
    public string? Description_JA { get; set; }

    [JsonPropertyName("description_ZH")]
    public string? Description_ZH { get; set; }

    [JsonPropertyName("audioUrl_VI")]
    public string? AudioUrl_VI { get; set; }

    [JsonPropertyName("audioUrl_EN")]
    public string? AudioUrl_EN { get; set; }

    [JsonPropertyName("audioUrl_KO")]
    public string? AudioUrl_KO { get; set; }

    [JsonPropertyName("audioUrl_JA")]
    public string? AudioUrl_JA { get; set; }

    [JsonPropertyName("audioUrl_ZH")]
    public string? AudioUrl_ZH { get; set; }

    [JsonPropertyName("radius")]
    public int? Radius { get; set; }

    [JsonPropertyName("priorityLevel")]
    public int? PriorityLevel { get; set; }

    [JsonPropertyName("boostPriority")]
    public int? BoostPriority { get; set; }

    [JsonPropertyName("boostExpiresAt")]
    public DateTime? BoostExpiresAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("approvalStatus")]
    public string? ApprovalStatus { get; set; }

    [JsonPropertyName("moderationStatus")]
    public string? ModerationStatus { get; set; }

    [JsonPropertyName("translationStatus")]
    public string? TranslationStatus { get; set; }

    [JsonPropertyName("contentVersion")]
    public int? ContentVersion { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("reviewedBy")]
    public string? ReviewedBy { get; set; }

    [JsonPropertyName("reviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; set; }

    [JsonPropertyName("countedQrScanCount")]
    public int? CountedQrScanCount { get; set; }

    [JsonPropertyName("replayQrScanCount")]
    public int? ReplayQrScanCount { get; set; }

    [JsonPropertyName("countedTtsPlayCount")]
    public int? CountedTtsPlayCount { get; set; }

    [JsonPropertyName("replayTtsPlayCount")]
    public int? ReplayTtsPlayCount { get; set; }

    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; set; }

    [JsonPropertyName("subscriptionPackage")]
    public string? SubscriptionPackage { get; set; }

    [JsonPropertyName("isPaid")]
    public bool? IsPaid { get; set; }

    [JsonPropertyName("lastTransactionId")]
    public string? LastTransactionId { get; set; }

    [JsonPropertyName("subscriptionExpiry")]
    public DateTime? SubscriptionExpiry { get; set; }

    [JsonPropertyName("merchantNote")]
    public string? MerchantNote { get; set; }

    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    private static string ToDisplayTag(string tag)
    {
        return (tag ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "oc dem" => "Ốc đêm",
            "an vat" => "Ăn vặt",
            "tra sua" => "Trà sữa",
            "nhau" => "Nhậu",
            "com bun" => "Cơm & bún",
            var value when !string.IsNullOrWhiteSpace(value) => value,
            _ => "Ăn vặt",
        };
    }
}

public class GeoLocation
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Point";

    [JsonPropertyName("coordinates")]
    public double[] Coordinates { get; set; } = Array.Empty<double>();
}
