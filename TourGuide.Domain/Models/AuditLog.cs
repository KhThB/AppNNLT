using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json;
namespace TourGuide.Domain.Models;

public class AuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    // Loại hành động: "QR_SCANNED", "POI_APPROVED", "USER_PREMIUM_UPGRADED"
    public string ActionType { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = ""; // Ai là người làm?

    public string TargetId { get; set; } = ""; // Tác động lên cái gì? (Ví dụ: ID của quán)

    // BsonDocument cho phép bạn ném bất kỳ dữ liệu JSON nào vào đây mà không cần định nghĩa trước
    public BsonDocument Details { get; set; } = new BsonDocument();

    public string? IPAddress { get; set; }  // Dùng IP để chặn spam quét QR

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}