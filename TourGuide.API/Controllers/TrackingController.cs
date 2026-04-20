using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace TourGuide.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        // Bộ nhớ tạm lưu trữ: [Mã thiết bị] -> [Thời gian Ping cuối cùng]
        private static readonly ConcurrentDictionary<string, DateTime> _activeUsers = new();

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
    }
}