using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TourGuide.Domain.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    // --- CÁC TRƯỜNG ĐĂNG NHẬP (Dành cho Merchant Portal) ---
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // --- CÁC TRƯỜNG THÔNG TIN CƠ BẢN ---
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Phân quyền: "Admin", "Merchant" (Chủ quán), "User" (Khách du lịch)
    // Để mặc định là Merchant cho trang đăng ký chủ quán
    public string Role { get; set; } = "Merchant";

    // --- TÍNH NĂNG B2C (Dành cho app Khách du lịch) ---
    public bool IsPremium { get; set; } = false;
    public DateTime? PremiumExpireDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}