using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;

namespace TourGuide.API.Services.Implementations;

public sealed class GoogleCloudTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly TranslationProviderOptions _options;

    public GoogleCloudTranslationProvider(HttpClient httpClient, IOptions<TranslationProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ProviderName => "GoogleCloud";

    public bool IsConfigured =>
        _options.Enabled &&
        IsSelectedProvider() &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Endpoint);

    public async Task<TranslationProviderResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Google Cloud Translation chưa được cấu hình.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationProviderResult { Text = string.Empty, Provider = ProviderName };
        }

        var endpoint = $"{Endpoint.TrimEnd('?')}";
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var requestUri = $"{endpoint}{separator}key={Uri.EscapeDataString(ApiKey)}";
        var payload = new
        {
            q = text,
            source = NormalizeLanguage(sourceLanguage),
            target = NormalizeLanguage(targetLanguage),
            format = "text",
        };

        using var response = await _httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<GoogleTranslateResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || result?.Data?.Translations.Count is null or 0)
        {
            var detail = result?.Error?.Message ?? response.ReasonPhrase ?? "Unknown Google Translation error";
            throw new InvalidOperationException($"Google Cloud Translation failed: {detail}");
        }

        return new TranslationProviderResult
        {
            Text = WebUtility.HtmlDecode(result.Data.Translations[0].TranslatedText ?? string.Empty),
            Provider = ProviderName,
        };
    }

    private string ApiKey => !string.IsNullOrWhiteSpace(_options.GoogleApiKey)
        ? _options.GoogleApiKey
        : _options.ApiKey;

    private string Endpoint => !string.IsNullOrWhiteSpace(_options.GoogleEndpoint)
        ? _options.GoogleEndpoint
        : _options.Endpoint;

    private bool IsSelectedProvider()
    {
        return string.Equals(_options.ProviderName, ProviderName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(_options.ProviderName, "Auto", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguage(string language)
    {
        return (language ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "vi" or "vn" => "vi",
            "en" => "en",
            "ko" or "kr" => "ko",
            "ja" or "jp" => "ja",
            "zh" or "cn" or "zh-cn" => "zh-CN",
            var value when !string.IsNullOrWhiteSpace(value) => value,
            _ => "vi",
        };
    }

    private sealed class GoogleTranslateResponse
    {
        [JsonPropertyName("data")]
        public GoogleTranslateData? Data { get; set; }

        [JsonPropertyName("error")]
        public GoogleTranslateError? Error { get; set; }
    }

    private sealed class GoogleTranslateData
    {
        [JsonPropertyName("translations")]
        public List<GoogleTranslation> Translations { get; set; } = new();
    }

    private sealed class GoogleTranslation
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }

    private sealed class GoogleTranslateError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
