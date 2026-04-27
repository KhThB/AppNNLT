using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers;

[ApiController]
[Authorize(Roles = KnownRoles.Admin)]
[Route("api")]
public sealed class OpsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly MongoCollections _collections;

    public OpsController(IAuditService auditService, MongoCollections collections)
    {
        _auditService = auditService;
        _collections = collections;
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<IReadOnlyList<AuditFeedItem>>> AuditLogs([FromQuery] int take = 25, CancellationToken cancellationToken = default)
    {
        return Ok(await _auditService.GetRecentAsync(Math.Clamp(take, 1, 100), cancellationToken));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<object>>> Sessions(CancellationToken cancellationToken)
    {
        var sessions = await _collections.UserSessions.Find(FilterDefinition<UserSession>.Empty)
            .SortByDescending(x => x.LastSeenAt)
            .Limit(50)
            .ToListAsync(cancellationToken);

        return Ok(sessions.Select(x => new
        {
            x.UserId,
            x.SessionId,
            x.Role,
            x.AuthProvider,
            x.IssuedAt,
            x.ExpiresAt,
            x.LastSeenAt,
            x.IsRevoked,
        }));
    }
}
