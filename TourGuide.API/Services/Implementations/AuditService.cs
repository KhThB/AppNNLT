using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Services.Abstractions;

namespace TourGuide.API.Services.Implementations;

public sealed class AuditService : IAuditService
{
    private readonly MongoCollections _collections;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(MongoCollections collections, IHttpContextAccessor httpContextAccessor)
    {
        _collections = collections;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task WriteAsync(
        string actionType,
        string targetType,
        string targetId,
        object? details = null,
        string? actorUserId = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var log = new TourGuide.Domain.Models.AuditLog
        {
            ActionType = actionType,
            ActorUserId = actorUserId ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            ActorRole = actorRole ?? principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            TargetType = targetType,
            TargetId = targetId,
            Details = ToBson(details),
            IPAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty,
            Timestamp = DateTime.UtcNow,
        };

        await _collections.AuditLogs.InsertOneAsync(log, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AuditFeedItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var items = await _collections.AuditLogs.Find(FilterDefinition<TourGuide.Domain.Models.AuditLog>.Empty)
            .SortByDescending(x => x.Timestamp)
            .Limit(take)
            .ToListAsync(cancellationToken);

        return items.Select(x => new AuditFeedItem
        {
            ActionType = x.ActionType,
            ActorRole = x.ActorRole,
            TargetType = x.TargetType,
            TargetId = x.TargetId,
            Description = x.Details.TryGetValue("message", out var message)
                ? message?.ToString() ?? x.ActionType
                : x.ActionType,
            Timestamp = x.Timestamp,
        }).ToList();
    }

    private static BsonDocument ToBson(object? details)
    {
        if (details == null)
        {
            return new BsonDocument();
        }

        try
        {
            return details.ToBsonDocument();
        }
        catch
        {
            return new BsonDocument("message", details.ToString());
        }
    }
}
