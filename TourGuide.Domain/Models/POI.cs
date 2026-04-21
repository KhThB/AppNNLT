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

    // --- CÁC BIẾN THỐNG KÊ THỰC TẾ (Đã được đưa vào trong class POI) ---
    public int QRScanCount { get; set; } = 0;
    public int TTSPlayCount { get; set; } = 0;
    public decimal Revenue { get; set; } = 0; // Doanh thu từ việc chủ quán trả phí "Boost" ưu tiên
    // Quản lý thanh toán
    public string SubscriptionPackage { get; set; } = "Basic"; // Basic, Premium, Pro
    public bool IsPaid { get; set; } = false;
    public string? LastTransactionId { get; set; }
    public DateTime? SubscriptionExpiry { get; set; }

    // Nội dung thuyết minh chủ quán tự nhập
    public string MerchantNote { get; set; } = "";
}

public class GeoLocation
{
    public string Type { get; set; } = "Point";
    public double[] Coordinates { get; set; }
}