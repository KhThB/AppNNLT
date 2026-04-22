using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Concurrent;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        // 1. Biến lưu trữ User Online (Lưu trong RAM - Code cũ của bạn)
        private static readonly ConcurrentDictionary<string, DateTime> _activeUsers = new();

        // 2. Kết nối MongoDB để cập nhật số lượt quét (Code mới)
        private readonly IMongoCollection<POI> _poiCollection;

        public TrackingController(IMongoDatabase database)
        {
            _poiCollection = database.GetCollection<POI>("POIs");
        }

        // ==========================================
        // NHÓM API 1: THEO DÕI USER ONLINE (APP)
        // ==========================================

        [HttpPost("ping/{deviceId}")]
        public IActionResult Ping(string deviceId)
        {
            // Ghi nhận thời điểm thiết bị này vừa báo cáo
            _activeUsers[deviceId] = DateTime.UtcNow;
            return Ok();
        }

        [HttpGet("online-count")]
        public IActionResult GetOnlineCount()
        {
            // Đếm những thiết bị vừa ping trong vòng 3 phút đổ lại
            var count = _activeUsers.Values.Count(lastSeen => (DateTime.UtcNow - lastSeen).TotalMinutes <= 3);
            return Ok(new { activeUsers = count });
        }


        // ==========================================
        // NHÓM API 2: GHI NHẬN TƯƠNG TÁC QUÁN ĂN (WEB QR)
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> LogAction([FromBody] TrackingRequest request)
        {
            if (string.IsNullOrEmpty(request.PoiId) || string.IsNullOrEmpty(request.ActionType))
                return BadRequest("Thiếu thông tin ID quán hoặc loại hành động.");

            // Tìm quán ăn dựa vào ID
            var filter = Builders<POI>.Filter.Eq(p => p.Id, request.PoiId);
            UpdateDefinition<POI> update = null!;

            // Toán tử $inc giúp cộng dồn an toàn ngay dưới Database, tránh lỗi ghi đè dữ liệu
            if (request.ActionType == "ScanQR")
            {
                update = Builders<POI>.Update.Inc(p => p.QRScanCount, 1);
            }
            else if (request.ActionType == "PlayTTS")
            {
                update = Builders<POI>.Update.Inc(p => p.TTSPlayCount, 1);
            }
            else
            {
                return BadRequest("Hành động không hợp lệ.");
            }

            // Gửi lệnh xuống MongoDB
            var result = await _poiCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount > 0)
                return Ok(new { message = "Ghi nhận thành công!" });

            return NotFound("Không tìm thấy quán ăn.");
        }
    }

    // Class phụ trợ để hứng dữ liệu từ WebQR gửi lên
    public class TrackingRequest
    {
        public string PoiId { get; set; } = "";
        public string ActionType { get; set; } = ""; // Truyền "ScanQR" hoặc "PlayTTS"
    }
}