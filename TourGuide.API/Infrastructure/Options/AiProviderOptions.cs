namespace TourGuide.API.Infrastructure.Options;

public sealed class TranslationProviderOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "ManualFallback";
}

public sealed class ModerationProviderOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ProviderName { get; set; } = "ManualFallback";
}
