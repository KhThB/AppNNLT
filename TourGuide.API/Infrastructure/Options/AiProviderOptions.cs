namespace TourGuide.API.Infrastructure.Options;

public sealed class TranslationProviderOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string GoogleApiKey { get; set; } = string.Empty;
    public string MyMemoryApiKey { get; set; } = string.Empty;
    public string MyMemoryEmail { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "Auto";
    public string Endpoint { get; set; } = "https://translation.googleapis.com/language/translate/v2";
    public string GoogleEndpoint { get; set; } = "https://translation.googleapis.com/language/translate/v2";
    public string MyMemoryEndpoint { get; set; } = "https://api.mymemory.translated.net/get";
}

public sealed class ModerationProviderOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "ManualFallback";
}
