using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Abstractions;

public interface IModerationService
{
    Task<string> EvaluateAsync(POI poi, CancellationToken cancellationToken = default);
}
