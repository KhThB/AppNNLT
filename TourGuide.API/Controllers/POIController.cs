using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TourGuide.Domain.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
namespace TourGuide.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class POIController : ControllerBase
    {
        private readonly IMongoCollection<POI> _poiCollection;
        private readonly IConfiguration _config;
        public POIController(IMongoDatabase database, IConfiguration config)
        {
            _poiCollection = database.GetCollection<POI>("POIs");
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePOI([FromBody] POI newPOI)
        {
            if (newPOI.Location == null || newPOI.Location.Coordinates.Length != 2)
            {
                return BadRequest("Tọa độ Location không hợp lệ.");
            }

            newPOI.Status = "Pending";
            newPOI.CreatedAt = DateTime.UtcNow;

            await _poiCollection.InsertOneAsync(newPOI);

            // Cập nhật: Trỏ về hàm GetById với route "details/{id}"
            return CreatedAtAction(nameof(GetById), new { id = newPOI.Id }, newPOI);
        }

        // --- NHÓM API LẤY DANH SÁCH (Static Routes) ---

        [HttpGet("approved")]
        public async Task<IActionResult> GetApprovedPOIs()
        {
            var filter = Builders<POI>.Filter.Eq(p => p.Status, "Approved");
            return Ok(await _poiCollection.Find(filter).ToListAsync());
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingPOIs()
        {
            var filter = Builders<POI>.Filter.Eq(p => p.Status, "Pending");
            return Ok(await _poiCollection.Find(filter).ToListAsync());
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyPOIs([FromQuery] double longitude, [FromQuery] double latitude, [FromQuery] double maxDistance = 5000)
        {
            var statusFilter = Builders<POI>.Filter.Eq(p => p.Status, "Approved");

            // SỬA LỖI: Với GeoJSON (Point), maxDistance tính bằng MÉT, không cần chia cho 6378100
            var geoFilter = Builders<POI>.Filter.NearSphere(
                p => p.Location.Coordinates,
                longitude,
                latitude,
                maxDistance);

            var combinedFilter = statusFilter & geoFilter;
            var nearbyPOIs = await _poiCollection.Find(combinedFilter).ToListAsync();

            var sortedPOIs = nearbyPOIs
                .OrderByDescending(p => p.PriorityLevel)
                .ThenBy(p => CalculateDistance(longitude, latitude, p.Location.Coordinates[0], p.Location.Coordinates[1]))
                .ToList();

            return Ok(sortedPOIs);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPOIById(string id)
        {
            // Tìm quán ăn trong Database dựa vào ID
            var poi = await _poiCollection.Find(p => p.Id == id).FirstOrDefaultAsync();

            if (poi == null)
            {
                return NotFound("Không tìm thấy dữ liệu quán ăn này.");
            }

            return Ok(poi);
        }

        // --- NHÓM API CHI TIẾT (Dynamic Routes) ---

        // GIỮ LẠI CÁI NÀY (Cách 1 bạn chọn): Để WebQR gọi mượt mà và không trùng với "pending/approved"
        [HttpGet("details/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var poi = await _poiCollection.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (poi == null) return NotFound();
            return Ok(poi);
        }

        // XÓA HÀM GetPOIById [HttpGet("{id}")] CŨ ĐI 
        // Vì nó chính là nguyên nhân gây lỗi Ambiguous (tranh chấp với "pending", "approved")

        [HttpGet("dashboard-stats")]
        public IActionResult GetDashboardStats()
        {
            var stats = new
            {
                TotalRevenue = 15000000,
                TotalQRScans = 1250,
                TotalTTSPlays = 3400,
                ChartLabels = new string[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" },
                ChartData = new double[] { 120, 150, 100, 200, 250, 300, 130 }
            };
            return Ok(stats);
        }

        // --- NHÓM API THAY ĐỔI TRẠNG THÁI (PUT) ---

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> ApprovePOI(string id)
        {
            var update = Builders<POI>.Update.Set(p => p.Status, "Approved");
            var result = await _poiCollection.UpdateOneAsync(p => p.Id == id, update);
            return result.ModifiedCount == 0 ? NotFound() : Ok("Đã duyệt.");
        }

        [HttpPut("{id}/reject")]
        public async Task<IActionResult> RejectPOI(string id)
        {
            var update = Builders<POI>.Update.Set(p => p.Status, "Rejected");
            var result = await _poiCollection.UpdateOneAsync(p => p.Id == id, update);
            return result.ModifiedCount == 0 ? NotFound() : Ok("Đã từ chối.");
        }

        private double CalculateDistance(double lon1, double lat1, double lon2, double lat2)
        {
            var r = 6371e3;
            var rad = Math.PI / 180;
            var dLat = (lat2 - lat1) * rad;
            var dLon = (lon2 - lon1) * rad;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePOI(string id, [FromBody] POI updatedPOI)
        {
            // Tìm quán xem có tồn tại không
            var existingPoi = await _poiCollection.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (existingPoi == null) return NotFound("Không tìm thấy quán ăn.");

            // Gộp tất cả các trường cần cập nhật vào 1 lệnh Update duy nhất
            var update = Builders<POI>.Update
                .Set(p => p.Name, updatedPOI.Name)
                .Set(p => p.Status, updatedPOI.Status)
                .Set(p => p.PriorityLevel, updatedPOI.PriorityLevel)
                .Set(p => p.ImageUrl, updatedPOI.ImageUrl) // Cập nhật ảnh
                .Set(p => p.Description_VI, updatedPOI.Description_VI) // Các trường mô tả
                .Set(p => p.Description_EN, updatedPOI.Description_EN)
                .Set(p => p.Description_KO, updatedPOI.Description_KO)
                .Set(p => p.Description_JA, updatedPOI.Description_JA)
                .Set(p => p.Description_ZH, updatedPOI.Description_ZH);

            var result = await _poiCollection.UpdateOneAsync(p => p.Id == id, update);

            return Ok(updatedPOI);
        }
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Không tìm thấy file ảnh.");

            // 1. Lấy chìa khóa từ appsettings.json
            var account = new Account(
                _config["Cloudinary:CloudName"],
                _config["Cloudinary:ApiKey"],
                _config["Cloudinary:ApiSecret"]
            );
            var cloudinary = new Cloudinary(account);

            // 2. Chuyển file thành dạng luồng (stream) để đưa lên mạng
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "VinhKhanhFoodTour" // Gom ảnh vào 1 thư mục cho gọn
            };

            // 3. Upload lên Cloudinary
            var uploadResult = await cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                return StatusCode(500, uploadResult.Error.Message);

            // 4. Trả về đường link vĩnh viễn (SecureUrl)
            return Ok(new { imageUrl = uploadResult.SecureUrl.ToString() });
        }
    }
}