using System.Security.Claims;
using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Abstractions;

public interface IBillingService
{
    Task<PremiumCheckoutResponse> CreatePremiumCheckoutAsync(PremiumCheckoutRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<PremiumCheckoutResponse> CreatePackageCheckoutAsync(PackageCheckoutRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingRecordResponse>> GetOwnerBillingAsync(string ownerId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingRecordResponse>> GetAdminBillingAsync(CancellationToken cancellationToken = default);
    BillingConfigStatusResponse GetConfigStatus();
    Task ProcessPayOsWebhookAsync(PayOsWebhookRequest request, CancellationToken cancellationToken = default);
}
