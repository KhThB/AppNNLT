using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

[BsonIgnoreExtraElements]
public class BillingRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string PoiId { get; set; } = string.Empty;

    public string BillingType { get; set; } = "Subscription";
    public string PackageName { get; set; } = SubscriptionPackages.Basic;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "Pending";
    public string Provider { get; set; } = "Manual";
    public long? PaymentOrderCode { get; set; }
    public string ProviderPaymentId { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public bool AutoRenew { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
