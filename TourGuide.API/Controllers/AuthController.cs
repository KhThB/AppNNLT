using CloudinaryDotNet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers
{
    // Removed the redundant outer AuthController class
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;
        private readonly IConfiguration _config;

        public AuthController(IMongoDatabase db, IConfiguration config)
        {
            _users = db.GetCollection<User>("Users");
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // Kiểm tra xem số điện thoại đã tồn tại chưa
            var existing = await _users.Find(u => u.Phone == user.Phone).FirstOrDefaultAsync();
            if (existing != null) return BadRequest("Số điện thoại đã được đăng ký.");

            // Băm mật khẩu trước khi lưu
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            await _users.InsertOneAsync(user);
            return Ok(new { message = "Đăng ký thành công." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _users.Find(u => u.Phone == request.Phone).FirstOrDefaultAsync();
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized("Sai số điện thoại hoặc mật khẩu.");

            // Tạo Token
            var token = GenerateJwtToken(user);

            // Trả về Token và thông tin cơ bản (không trả về PasswordHash)
            return Ok(new
            {
                Token = token,
                User = new { user.Id, user.FullName, user.Phone, user.Role }
            });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _config["Jwt:Key"];
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Gắn thông tin nhận diện vào trong Token
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.Name, user.Phone),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7), // Token sống 7 ngày
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        [HttpPost("social-login")]
        public async Task<IActionResult> SocialLogin([FromBody] SocialLoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Provider) || string.IsNullOrEmpty(request.ProviderId))
                return BadRequest("Thiếu thông tin đăng nhập mxh.");

            // 1. Kiểm tra xem user này đã tồn tại chưa (dựa trên ProviderId)
            var user = await _users.Find(u => u.ProviderId == request.ProviderId && u.AuthProvider == request.Provider).FirstOrDefaultAsync();

            // 2. Nếu chưa tồn tại, tự động tạo tài khoản mới cho họ
            if (user == null)
            {
                user = new User
                {
                    AuthProvider = request.Provider,
                    ProviderId = request.ProviderId,
                    FullName = request.FullName ?? "Người dùng ẩn danh",
                    Email = request.Email ?? "",
                    Role = "User" // Luôn set là khách du lịch
                };
                await _users.InsertOneAsync(user);
            }

            // 3. Cấp Token của hệ thống
            var token = GenerateJwtToken(user);
            return Ok(new
            {
                Token = token,
                User = new { user.Id, user.FullName, user.Email, user.Role, user.AuthProvider }
            });
        }
    }

    // Lớp phụ để hứng dữ liệu đăng nhập
    public class LoginRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SocialLoginRequest
    {
        public string Provider { get; set; } = "Google"; // Google / Facebook / Apple
        public string ProviderId { get; set; } = string.Empty; // Mã ID định danh từ bên thứ 3
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }
}