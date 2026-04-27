using Microsoft.AspNetCore.Mvc;
using TourGuide.API.Contracts;
using TourGuide.API.Services.Abstractions;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api/tracking")]
public sealed class TrackingController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public TrackingController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromBody] PingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest("Missing deviceId.");
        }

        await _analyticsService.RecordPingAsync(request, cancellationToken);
        return Ok();
    }

    [HttpGet("online-count")]
    public IActionResult OnlineCount()
    {
        return Ok(new { activeUsers = _analyticsService.GetActiveUserCount() });
    }
}
