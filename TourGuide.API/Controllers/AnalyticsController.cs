using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IMongoCollection<POI> _poiCollection;
        private readonly IMongoCollection<NarrationLog> _narrationLogCollection;

        public AnalyticsController(IMongoDatabase database)
        {
            _poiCollection = database.GetCollection<POI>("POIs");
            _narrationLogCollection = database.GetCollection<NarrationLog>("NarrationLogs");
        }

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            // Lấy toàn bộ quán ăn đã duyệt
            var approvedPOIs = await _poiCollection.Find(p => p.Status == "Approved").ToListAsync();

            // Tính toán tổng số lượng
            var totalRevenue = approvedPOIs.Sum(p => p.Revenue);
            var totalQRScans = approvedPOIs.Sum(p => p.QRScanCount);
            var totalTTSPlays = approvedPOIs.Sum(p => p.TTSPlayCount);

            // Tính DwellTime trung bình từ NarrationLog
            var logs = await _narrationLogCollection.Find(_ => true).ToListAsync();
            string avgDwellTimeStr = "0s";
            if (logs.Any())
            {
                var avgSeconds = logs.Average(x => x.DwellTime);
                var ts = TimeSpan.FromSeconds(avgSeconds);
                avgDwellTimeStr = $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            }

            var stats = new
            {
                TotalRevenue = totalRevenue,
                TotalQRScans = totalQRScans,
                TotalTTSPlays = totalTTSPlays,
                AverageDwellTime = avgDwellTimeStr,

                ChartLabels = new string[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" },
                ChartData = new double[] { 10, 15, 20, 10, 25, 30, totalQRScans > 0 ? totalQRScans : 50 }
            };
            
            return Ok(stats);
        }

        [HttpPost("narration-log")]
        public async Task<IActionResult> LogNarration([FromBody] NarrationLogRequest request)
        {
            if (string.IsNullOrEmpty(request.PoiId) || string.IsNullOrEmpty(request.UserId))
                return BadRequest("Thiếu PoiId hoặc UserId");

            var log = new NarrationLog
            {
                PoiId = request.PoiId,
                UserId = request.UserId,
                OwnerId = request.OwnerId,
                DwellTime = request.DwellTime,
                StartedAt = request.StartedAt,
                EndedAt = request.EndedAt,
                CreatedAt = DateTime.UtcNow
            };

            await _narrationLogCollection.InsertOneAsync(log);
            return Ok(new { message = "Ghi nhận DwellTime thành công." });
        }
    }

    public class NarrationLogRequest
    {
        public string PoiId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string? OwnerId { get; set; }
        public int DwellTime { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
    }
}
