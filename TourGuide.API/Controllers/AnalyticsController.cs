using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourGuide.API.Contracts;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpPost("qr/scan")]
    public async Task<ActionResult<QrScanResponse>> RecordQrScan([FromBody] QrScanRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.RecordQrScanAsync(request, cancellationToken));
    }

    [HttpPost("narration/play")]
    public async Task<ActionResult<NarrationPlayResponse>> StartNarration([FromBody] NarrationPlayRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.StartNarrationAsync(request, cancellationToken));
    }

    [HttpPost("narration/finish")]
    public async Task<IActionResult> FinishNarration([FromBody] NarrationFinishRequest request, CancellationToken cancellationToken)
    {
        await _analyticsService.FinishNarrationAsync(request, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpGet("analytics/admin/overview")]
    public async Task<ActionResult<AdminOverviewResponse>> AdminOverview(CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.GetAdminOverviewAsync(cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpGet("analytics/admin/heatmap")]
    public async Task<ActionResult<IReadOnlyList<HeatmapPoint>>> Heatmap([FromQuery] int hours = 4, CancellationToken cancellationToken = default)
    {
        return Ok(await _analyticsService.GetHeatmapAsync(hours, cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpGet("analytics/owner/overview")]
    public async Task<ActionResult<OwnerOverviewResponse>> OwnerOverview([FromQuery] string? ownerId, CancellationToken cancellationToken)
    {
        var resolvedOwnerId = ownerId;
        if (User.IsInRole(KnownRoles.Merchant))
        {
            resolvedOwnerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }

        if (string.IsNullOrWhiteSpace(resolvedOwnerId))
        {
            return BadRequest("Thiếu ownerId.");
        }

        return Ok(await _analyticsService.GetOwnerOverviewAsync(resolvedOwnerId, cancellationToken));
    }
}
