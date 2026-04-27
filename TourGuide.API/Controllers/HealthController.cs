using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IMongoDatabase _database;

    public HealthController(IMongoDatabase database)
    {
        _database = database;
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "live", utcNow = DateTime.UtcNow });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        await _database.ListCollectionNames().FirstOrDefaultAsync(cancellationToken);
        return Ok(new { status = "ready", database = _database.DatabaseNamespace.DatabaseName });
    }
}
