using TourGuide.API.Contracts;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Abstractions;

public interface ITranslationService
{
    Task<TranslationPreviewResponse> PreviewAsync(TranslationPreviewRequest request, CancellationToken cancellationToken = default);
    Task RefreshAsync(POI poi, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, PoiLocalizedContent>> ResolveForPublicAsync(POI poi, CancellationToken cancellationToken = default);
    string CalculateSourceHash(string sourceText);
}
