using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;

namespace TourGuide.API.Services.Implementations;

public sealed class CompositeTranslationProvider : ITranslationProvider
{
    private readonly GoogleCloudTranslationProvider _googleCloudProvider;
    private readonly MyMemoryTranslationProvider _myMemoryProvider;
    private readonly TranslationProviderOptions _options;

    public CompositeTranslationProvider(
        GoogleCloudTranslationProvider googleCloudProvider,
        MyMemoryTranslationProvider myMemoryProvider,
        IOptions<TranslationProviderOptions> options)
    {
        _googleCloudProvider = googleCloudProvider;
        _myMemoryProvider = myMemoryProvider;
        _options = options.Value;
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_options.ProviderName)
        ? "Auto"
        : _options.ProviderName;

    public bool IsConfigured => OrderedProviders().Any(x => x.IsConfigured);

    public async Task<TranslationProviderResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        foreach (var provider in OrderedProviders().Where(x => x.IsConfigured))
        {
            try
            {
                return await provider.TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
            }
            catch (Exception ex)
            {
                errors.Add($"{provider.ProviderName}: {ex.Message}");
            }
        }

        var detail = errors.Count == 0
            ? "No translation provider is configured."
            : string.Join("; ", errors);
        throw new InvalidOperationException(detail);
    }

    private IEnumerable<ITranslationProvider> OrderedProviders()
    {
        if (string.Equals(_options.ProviderName, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
        {
            yield return _googleCloudProvider;
            yield break;
        }

        if (string.Equals(_options.ProviderName, "MyMemory", StringComparison.OrdinalIgnoreCase))
        {
            yield return _myMemoryProvider;
            yield break;
        }

        yield return _googleCloudProvider;
        yield return _myMemoryProvider;
    }
}
