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
        // 1. Biến lưu trữ User Online (RAM) - Giúp đếm Dashboard siêu tốc
        private static readonly ConcurrentDictionary<string, DateTime> _activeUsers = new();

        // 2. Kết nối MongoDB
        private readonly IMongoCollection<POI> _poiCollection;
        private readonly IMongoCollection<TrackingData> _trackingCollection; // Thêm Collection mới

        public TrackingController(IMongoDatabase database)
        {
            _poiCollection = database.GetCollection<POI>("POIs");
            _trackingCollection = database.GetCollection<TrackingData>("TrackingData");
        }

        // ==========================================
        // NHÓM API 1: THEO DÕI USER ONLINE & GPS (APP)
        // ==========================================

        [HttpPost("ping")]
        public async Task<IActionResult> Ping([FromBody] PingRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceId)) return BadRequest("Missing DeviceId");

            // 1. Cập nhật thời điểm ping vào RAM để đếm Online nhanh
            _activeUsers[request.DeviceId] = DateTime.UtcNow;

            // 2. Lưu vết định vị xuống MongoDB để phục vụ vẽ Heatmap sau này
            if (request.Latitude != 0 && request.Longitude != 0)
            {
                var trackingRecord = new TrackingData
                {
                    UserId = request.DeviceId, // Có thể map với UserId thật nếu đã login
                    Location = new GeoLocation
                    {
                        Type = "Point",
                        Coordinates = new double[] { request.Longitude, request.Latitude }
                    },
                    Speed = request.Speed,
                    Timestamp = DateTime.UtcNow
                };

                // Lưu vào database ngầm (fire and forget có thể cân nhắc để tăng tốc, nhưng tạm thời dùng await)
                await _trackingCollection.InsertOneAsync(trackingRecord);
            }

            return Ok();
        }

        [HttpGet("online-count")]
        public IActionResult GetOnlineCount()
        {
            // Đếm thiết bị vừa ping trong vòng 10 giây (Ping chu kỳ 5s)
            var count = _activeUsers.Values.Count(lastSeen =>
                (DateTime.UtcNow - lastSeen).TotalSeconds <= 10);

            return Ok(new { activeUsers = count });
        }

        // ==========================================
        // NHÓM API 2: GHI NHẬN TƯƠNG TÁC QUÁN ĂN (WEB QR)
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> LogAction([FromBody] TrackingRequest request)
        {
            if (string.IsNullOrEmpty(request.PoiId) || string.IsNullOrEmpty(request.ActionType))
                return BadRequest("Thiếu thông tin.");

            var filter = Builders<POI>.Filter.Eq(p => p.Id, request.PoiId);
            UpdateDefinition<POI> update = null!;

            if (request.ActionType == "ScanQR")
                update = Builders<POI>.Update.Inc(p => p.QRScanCount, 1);
            else if (request.ActionType == "PlayTTS")
                update = Builders<POI>.Update.Inc(p => p.TTSPlayCount, 1);
            else
                return BadRequest("Hành động không hợp lệ.");

            var result = await _poiCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount > 0)
                return Ok(new { message = "Ghi nhận thành công!" });

            return NotFound("Không tìm thấy quán.");
        }
    }

    public class TrackingRequest
    {
        public string PoiId { get; set; } = "";
        public string ActionType { get; set; } = "";
    }

    public class PingRequest
    {
        public string DeviceId { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
    }
}