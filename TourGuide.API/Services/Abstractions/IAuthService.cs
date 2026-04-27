using System.Security.Claims;
using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Abstractions;

public interface IAuthService
{
    Task EnsureBootstrapAdminAsync(CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string requiredRole, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterMerchantAsync(MerchantRegisterRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<SessionUserResponse?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> IsSessionActiveAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<AuthResponse> AcceptSocialContractAsync(SocialLoginContract request, CancellationToken cancellationToken = default);
}
