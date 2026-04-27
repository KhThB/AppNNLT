using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Abstractions;

public interface IAuditService
{
    Task WriteAsync(
        string actionType,
        string targetType,
        string targetId,
        object? details = null,
        string? actorUserId = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditFeedItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
