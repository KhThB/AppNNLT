using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace TourGuide.Domain.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public string Email { get; set; }
    public string FullName { get; set; }

    // Phân quyền: "Admin", "Owner" (Chủ quán), "User" (Khách du lịch)
    public string Role { get; set; }

    // --- Tính năng Moi tiền B2C ---
    public bool IsPremium { get; set; }
    public DateTime? PremiumExpireDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}