using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public sealed class TranslationService : ITranslationService
{
    private static readonly string[] Languages = ["VI", "EN", "KO", "JA", "ZH"];
    private readonly MongoCollections _collections;
    private readonly ITranslationProvider _translationProvider;
    private readonly TranslationProviderOptions _options;

    public TranslationService(
        MongoCollections collections,
        ITranslationProvider translationProvider,
        IOptions<TranslationProviderOptions> options)
    {
        _collections = collections;
        _translationProvider = translationProvider;
        _options = options.Value;
    }

    public async Task<TranslationPreviewResponse> PreviewAsync(
        TranslationPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceText = (request.SourceDescription ?? string.Empty).Trim();
        var sourceLanguage = string.IsNullOrWhiteSpace(request.SourceLanguage)
            ? "vi"
            : request.SourceLanguage.Trim();
        var targetLanguages = request.TargetLanguages ?? Array.Empty<string>();
        var requestedLanguages = targetLanguages.Count > 0
            ? targetLanguages.Select(BusinessRules.NormalizeLanguageCode)
                .Where(x => Languages.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Languages;

        var contents = new Dictionary<string, PoiLocalizedContent>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in requestedLanguages)
        {
            if (language == "VI")
            {
                contents[language] = new PoiLocalizedContent
                {
                    Description = sourceText,
                    Status = TranslationStatuses.Ready,
                };
                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                contents[language] = new PoiLocalizedContent
                {
                    Description = string.Empty,
                    Status = TranslationStatuses.Ready,
                };
                continue;
            }

            if (!_translationProvider.IsConfigured)
            {
                contents[language] = new PoiLocalizedContent
                {
                    Description = string.Empty,
                    Status = TranslationStatuses.PendingManual,
                    ErrorMessage = "Translation provider is not configured.",
                };
                continue;
            }

            try
            {
                var translated = await _translationProvider.TranslateAsync(
                    sourceText,
                    sourceLanguage,
                    language,
                    cancellationToken);

                contents[language] = new PoiLocalizedContent
                {
                    Description = translated.Text,
                    Status = TranslationStatuses.Ready,
                };
            }
            catch (Exception ex)
            {
                contents[language] = new PoiLocalizedContent
                {
                    Description = string.Empty,
                    Status = TranslationStatuses.PendingManual,
                    ErrorMessage = ex.Message,
                };
            }
        }

        return new TranslationPreviewResponse
        {
            SourceLanguage = sourceLanguage,
            SourceHash = CalculateSourceHash(sourceText),
            IsProviderConfigured = _translationProvider.IsConfigured,
            Contents = contents,
        };
    }

    public async Task RefreshAsync(POI poi, CancellationToken cancellationToken = default)
    {
        var sourceHash = CalculateSourceHash(poi.SourceDescription);
        var allReady = true;
        foreach (var language in Languages)
        {
            var result = await ResolveTranslationAsync(poi, language, cancellationToken);
            ApplyTranslatedText(poi, language, result.Text, result.Status);
            if (result.Status != TranslationStatuses.Ready)
            {
                allReady = false;
            }

            var update = Builders<TranslationCache>.Update
                .Set(x => x.PoiId, poi.Id)
                .Set(x => x.ContentVersion, poi.ContentVersion)
                .Set(x => x.SourceHash, sourceHash)
                .Set(x => x.SourceLanguage, poi.SourceLanguage)
                .Set(x => x.TargetLanguage, language)
                .Set(x => x.TranslatedText, result.Text)
                .Set(x => x.Status, result.Status)
                .Set(x => x.Provider, result.Provider)
                .Set(x => x.ErrorMessage, result.ErrorMessage)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow);

            await _collections.TranslationCaches.UpdateOneAsync(
                x => x.PoiId == poi.Id && x.TargetLanguage == language,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }

        poi.TranslationStatus = allReady ? TranslationStatuses.Ready : TranslationStatuses.PendingManual;
        await _collections.Pois.UpdateOneAsync(
            x => x.Id == poi.Id,
            Builders<POI>.Update
                .Set(x => x.Description_VI, poi.Description_VI)
                .Set(x => x.Description_EN, poi.Description_EN)
                .Set(x => x.Description_KO, poi.Description_KO)
                .Set(x => x.Description_JA, poi.Description_JA)
                .Set(x => x.Description_ZH, poi.Description_ZH)
                .Set(x => x.TranslationStatus, poi.TranslationStatus)
                .Set(x => x.UpdatedAt, poi.UpdatedAt),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, PoiLocalizedContent>> ResolveForPublicAsync(POI poi, CancellationToken cancellationToken = default)
    {
        var entries = await _collections.TranslationCaches.Find(x => x.PoiId == poi.Id && x.ContentVersion == poi.ContentVersion)
            .ToListAsync(cancellationToken);

        if (entries.Count < Languages.Length)
        {
            await RefreshAsync(poi, cancellationToken);
            entries = await _collections.TranslationCaches.Find(x => x.PoiId == poi.Id && x.ContentVersion == poi.ContentVersion)
                .ToListAsync(cancellationToken);
        }

        var map = new Dictionary<string, PoiLocalizedContent>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in Languages)
        {
            var entry = entries.FirstOrDefault(x => x.TargetLanguage == language);
            var (fallbackText, fallbackStatus) = GetTextForLanguage(poi, language);
            map[language] = new PoiLocalizedContent
            {
                Description = entry?.TranslatedText ?? fallbackText,
                AudioUrl = GetAudioUrl(poi, language),
                Status = entry?.Status ?? fallbackStatus,
                ErrorMessage = entry?.ErrorMessage ?? string.Empty,
            };
        }

        return map;
    }

    public string CalculateSourceHash(string sourceText)
    {
        return BusinessRules.CalculateHash(sourceText);
    }

    private async Task<ResolvedTranslation> ResolveTranslationAsync(POI poi, string language, CancellationToken cancellationToken)
    {
        if (language == "VI")
        {
            var text = string.IsNullOrWhiteSpace(poi.Description_VI) ? poi.SourceDescription : poi.Description_VI;
            return new ResolvedTranslation(text, TranslationStatuses.Ready, "Source", string.Empty);
        }

        if (string.IsNullOrWhiteSpace(poi.SourceDescription))
        {
            return new ResolvedTranslation(string.Empty, TranslationStatuses.Ready, "EmptySource", string.Empty);
        }

        var existingText = GetExistingDescription(poi, language);
        if (!string.IsNullOrWhiteSpace(existingText))
        {
            return new ResolvedTranslation(existingText, TranslationStatuses.Ready, "MerchantProvided", string.Empty);
        }

        if (!_translationProvider.IsConfigured)
        {
            var (fallbackText, fallbackStatus) = GetTextForLanguage(poi, language);
            return new ResolvedTranslation(fallbackText, fallbackStatus, "ManualFallback", "Translation provider is not configured.");
        }

        try
        {
            var translated = await _translationProvider.TranslateAsync(
                poi.SourceDescription,
                poi.SourceLanguage,
                language,
                cancellationToken);

            return new ResolvedTranslation(translated.Text, TranslationStatuses.Ready, translated.Provider, string.Empty);
        }
        catch (Exception ex)
        {
            var (fallbackText, fallbackStatus) = GetTextForLanguage(poi, language);
            return new ResolvedTranslation(fallbackText, fallbackStatus, _options.ProviderName, ex.Message);
        }
    }

    private static (string Text, string Status) GetTextForLanguage(POI poi, string language)
    {
        return language switch
        {
            "VI" => (string.IsNullOrWhiteSpace(poi.Description_VI) ? poi.SourceDescription : poi.Description_VI, TranslationStatuses.Ready),
            "EN" => BusinessRules.ResolveTranslationText(poi.Description_EN, poi.SourceDescription),
            "KO" => BusinessRules.ResolveTranslationText(poi.Description_KO, poi.SourceDescription),
            "JA" => BusinessRules.ResolveTranslationText(poi.Description_JA, poi.SourceDescription),
            "ZH" => BusinessRules.ResolveTranslationText(poi.Description_ZH, poi.SourceDescription),
            _ => (poi.SourceDescription, TranslationStatuses.PendingManual),
        };
    }

    private static void ApplyTranslatedText(POI poi, string language, string text, string status)
    {
        if (status != TranslationStatuses.Ready)
        {
            return;
        }

        switch (language)
        {
            case "VI":
                poi.Description_VI = text;
                break;
            case "EN":
                poi.Description_EN = text;
                break;
            case "KO":
                poi.Description_KO = text;
                break;
            case "JA":
                poi.Description_JA = text;
                break;
            case "ZH":
                poi.Description_ZH = text;
                break;
        }
    }

    private static string GetAudioUrl(POI poi, string language)
    {
        return language switch
        {
            "VI" => poi.AudioUrl_VI,
            "EN" => poi.AudioUrl_EN,
            "KO" => poi.AudioUrl_KO,
            "JA" => poi.AudioUrl_JA,
            "ZH" => poi.AudioUrl_ZH,
            _ => string.Empty,
        };
    }

    private static string GetExistingDescription(POI poi, string language)
    {
        return language switch
        {
            "EN" => poi.Description_EN,
            "KO" => poi.Description_KO,
            "JA" => poi.Description_JA,
            "ZH" => poi.Description_ZH,
            _ => string.Empty,
        };
    }

    private sealed record ResolvedTranslation(string Text, string Status, string Provider, string ErrorMessage);
}
