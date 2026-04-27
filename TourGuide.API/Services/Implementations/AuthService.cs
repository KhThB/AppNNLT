using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public sealed class AuthService : IAuthService
{
    private readonly MongoCollections _collections;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AdminBootstrapOptions _bootstrapOptions;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        MongoCollections collections,
        IJwtTokenService jwtTokenService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AdminBootstrapOptions> bootstrapOptions,
        IOptions<JwtSettings> jwtSettings)
    {
        _collections = collections;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _bootstrapOptions = bootstrapOptions.Value;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task EnsureBootstrapAdminAsync(CancellationToken cancellationToken = default)
    {
        if (!_bootstrapOptions.Enabled)
        {
            return;
        }

        var configuredPhone = _bootstrapOptions.Phone.Trim();
        var bootstrapAdmin = await _collections.Users.Find(x => x.Phone == configuredPhone).FirstOrDefaultAsync(cancellationToken);
        if (bootstrapAdmin != null)
        {
            var update = Builders<User>.Update
                .Set(x => x.FullName, string.IsNullOrWhiteSpace(bootstrapAdmin.FullName) ? _bootstrapOptions.FullName : bootstrapAdmin.FullName)
                .Set(x => x.Email, string.IsNullOrWhiteSpace(bootstrapAdmin.Email) ? _bootstrapOptions.Email : bootstrapAdmin.Email)
                .Set(x => x.Role, KnownRoles.Admin)
                .Set(x => x.AuthProvider, "Local")
                .Set(x => x.ProviderId, configuredPhone)
                .Set(x => x.PasswordHash, BCrypt.Net.BCrypt.HashPassword(_bootstrapOptions.Password))
                .Set(x => x.IsActive, true);

            await _collections.Users.UpdateOneAsync(x => x.Id == bootstrapAdmin.Id, update, cancellationToken: cancellationToken);
            return;
        }

        var admin = new User
        {
            FullName = _bootstrapOptions.FullName,
            Phone = configuredPhone,
            Email = _bootstrapOptions.Email,
            Role = KnownRoles.Admin,
            AuthProvider = "Local",
            ProviderId = configuredPhone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(_bootstrapOptions.Password),
            CreatedAt = DateTime.UtcNow,
        };

        await _collections.Users.InsertOneAsync(admin, cancellationToken: cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string requiredRole, CancellationToken cancellationToken = default)
    {
        var user = await _collections.Users.Find(x => x.Phone == request.Phone).FirstOrDefaultAsync(cancellationToken);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Sai số điện thoại hoặc mật khẩu.");
        }

        if (!string.Equals(user.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tài khoản không có quyền truy cập cổng này.");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Tài khoản đã bị khóa.");
        }

        var session = await CreateSessionAsync(user, cancellationToken);
        user.LastLoginAt = DateTime.UtcNow;
        await _collections.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);

        await _auditService.WriteAsync(
            "AUTH_LOGIN",
            "User",
            user.Id,
            new { message = $"{requiredRole} logged in", sessionId = session.SessionId },
            user.Id,
            user.Role,
            cancellationToken);

        return new AuthResponse
        {
            Token = _jwtTokenService.CreateToken(user, session.SessionId),
            User = ToSessionUser(user, session.SessionId),
        };
    }

    public async Task<AuthResponse> RegisterMerchantAsync(MerchantRegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = request.Phone.Trim();
        var existing = await _collections.Users.Find(x => x.Phone == normalizedPhone).FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException("Số điện thoại đã được đăng ký.");
        }

        var user = new User
        {
            FullName = request.FullName,
            Phone = normalizedPhone,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = KnownRoles.Merchant,
            AuthProvider = "Local",
            ProviderId = normalizedPhone,
            CreatedAt = DateTime.UtcNow,
        };

        await _collections.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        var session = await CreateSessionAsync(user, cancellationToken);

        await _auditService.WriteAsync(
            "MERCHANT_REGISTERED",
            "User",
            user.Id,
            new { message = "Merchant registered and started session", sessionId = session.SessionId },
            user.Id,
            user.Role,
            cancellationToken);

        return new AuthResponse
        {
            Token = _jwtTokenService.CreateToken(user, session.SessionId),
            User = ToSessionUser(user, session.SessionId),
        };
    }

    public async Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var sessionId = principal.FindFirstValue("sessionId");
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var update = Builders<UserSession>.Update
            .Set(x => x.IsRevoked, true)
            .Set(x => x.RevokedAt, DateTime.UtcNow);

        await _collections.UserSessions.UpdateOneAsync(
            x => x.SessionId == sessionId,
            update,
            cancellationToken: cancellationToken);

        await _auditService.WriteAsync(
            "AUTH_LOGOUT",
            "User",
            userId,
            new { message = "User logged out", sessionId },
            userId,
            principal.FindFirstValue(ClaimTypes.Role),
            cancellationToken);
    }

    public async Task<SessionUserResponse?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = principal.FindFirstValue("sessionId");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var isActive = await IsSessionActiveAsync(sessionId, cancellationToken);
        if (!isActive)
        {
            return null;
        }

        var user = await _collections.Users.Find(x => x.Id == userId).FirstOrDefaultAsync(cancellationToken);
        return user == null ? null : ToSessionUser(user, sessionId);
    }

    public async Task<bool> IsSessionActiveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _collections.UserSessions.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync(cancellationToken);
        if (session == null || session.IsRevoked || session.ExpiresAt <= DateTime.UtcNow)
        {
            return false;
        }

        await _collections.UserSessions.UpdateOneAsync(
            x => x.SessionId == sessionId,
            Builders<UserSession>.Update.Set(x => x.LastSeenAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<AuthResponse> AcceptSocialContractAsync(SocialLoginContract request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderId))
        {
            throw new InvalidOperationException("Thiếu thông tin đăng nhập mạng xã hội.");
        }

        var user = await _collections.Users.Find(x => x.AuthProvider == request.Provider && x.ProviderId == request.ProviderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            user = new User
            {
                AuthProvider = request.Provider,
                ProviderId = request.ProviderId,
                FullName = string.IsNullOrWhiteSpace(request.FullName) ? "Social User" : request.FullName,
                Email = request.Email,
                Role = KnownRoles.User,
                CreatedAt = DateTime.UtcNow,
            };

            await _collections.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        }

        var session = await CreateSessionAsync(user, cancellationToken);
        return new AuthResponse
        {
            Token = _jwtTokenService.CreateToken(user, session.SessionId),
            User = ToSessionUser(user, session.SessionId),
        };
    }

    private async Task<UserSession> CreateSessionAsync(User user, CancellationToken cancellationToken)
    {
        var session = new UserSession
        {
            UserId = user.Id,
            Role = user.Role,
            SessionId = Guid.NewGuid().ToString("N"),
            AuthProvider = user.AuthProvider,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.ExpiryDays),
            LastSeenAt = DateTime.UtcNow,
            IPAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty,
        };

        await _collections.UserSessions.InsertOneAsync(session, cancellationToken: cancellationToken);
        return session;
    }

    private static SessionUserResponse ToSessionUser(User user, string sessionId)
    {
        return new SessionUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Phone = user.Phone,
            Email = user.Email,
            Role = user.Role,
            SessionId = sessionId,
        };
    }
}
