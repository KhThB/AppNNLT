using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public sealed class ModerationService : IModerationService
{
    private static readonly string[] BlockedTokens = ["18+", "violence", "hate", "weapon", "spam"];
    private readonly ModerationProviderOptions _options;

    public ModerationService(IOptions<ModerationProviderOptions> options)
    {
        _options = options.Value;
    }

    public Task<string> EvaluateAsync(POI poi, CancellationToken cancellationToken = default)
    {
        var source = $"{poi.Name} {poi.SourceDescription} {poi.MerchantNote}".ToLowerInvariant();

        if (BlockedTokens.Any(source.Contains))
        {
            return Task.FromResult(ModerationStatuses.Rejected);
        }

        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Task.FromResult(ModerationStatuses.Approved);
        }

        return Task.FromResult(ModerationStatuses.PendingManual);
    }
}
