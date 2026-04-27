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
    private readonly TranslationProviderOptions _options;

    public TranslationService(MongoCollections collections, IOptions<TranslationProviderOptions> options)
    {
        _collections = collections;
        _options = options.Value;
    }

    public async Task RefreshAsync(POI poi, CancellationToken cancellationToken = default)
    {
        var sourceHash = CalculateSourceHash(poi.SourceDescription);
        foreach (var language in Languages)
        {
            var (text, status) = GetTextForLanguage(poi, language);

            var update = Builders<TranslationCache>.Update
                .Set(x => x.PoiId, poi.Id)
                .Set(x => x.ContentVersion, poi.ContentVersion)
                .Set(x => x.SourceHash, sourceHash)
                .Set(x => x.SourceLanguage, poi.SourceLanguage)
                .Set(x => x.TargetLanguage, language)
                .Set(x => x.TranslatedText, text)
                .Set(x => x.Status, status)
                .Set(x => x.Provider, _options.Enabled ? _options.ProviderName : "ManualFallback")
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow);

            await _collections.TranslationCaches.UpdateOneAsync(
                x => x.PoiId == poi.Id && x.TargetLanguage == language,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }
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
            };
        }

        return map;
    }

    public string CalculateSourceHash(string sourceText)
    {
        return BusinessRules.CalculateHash(sourceText);
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
}
