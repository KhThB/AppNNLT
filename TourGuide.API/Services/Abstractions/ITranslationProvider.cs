namespace TourGuide.API.Services.Abstractions;

public interface ITranslationProvider
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<TranslationProviderResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}

public sealed class TranslationProviderResult
{
    public string Text { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}
