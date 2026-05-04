using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourGuide.API.Contracts;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api/billing")]
public sealed class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpGet("config-status")]
    public ActionResult<BillingConfigStatusResponse> ConfigStatus()
    {
        return Ok(_billingService.GetConfigStatus());
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPost("premium/checkout")]
    public async Task<ActionResult<PremiumCheckoutResponse>> CreatePremiumCheckout([FromBody] PremiumCheckoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billingService.CreatePremiumCheckoutAsync(request, User, cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPost("package/checkout")]
    public async Task<ActionResult<PremiumCheckoutResponse>> CreatePackageCheckout([FromBody] PackageCheckoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billingService.CreatePackageCheckoutAsync(request, User, cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<IReadOnlyList<BillingRecordResponse>>> OwnerBilling(string ownerId, CancellationToken cancellationToken)
    {
        return Ok(await _billingService.GetOwnerBillingAsync(ownerId, User, cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpGet("admin/records")]
    public async Task<ActionResult<IReadOnlyList<BillingRecordResponse>>> AdminBilling(CancellationToken cancellationToken)
    {
        return Ok(await _billingService.GetAdminBillingAsync(cancellationToken));
    }

    [HttpPost("webhook/payos")]
    public async Task<IActionResult> PayOsWebhook([FromBody] PayOsWebhookRequest request, CancellationToken cancellationToken)
    {
        await _billingService.ProcessPayOsWebhookAsync(request, cancellationToken);
        return Ok(new { received = true });
    }
}
