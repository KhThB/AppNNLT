using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;

namespace TourGuide.API.Services.Implementations;

public sealed class MyMemoryTranslationProvider : ITranslationProvider
{
    private const int MaxSegmentBytes = 450;
    private readonly HttpClient _httpClient;
    private readonly TranslationProviderOptions _options;

    public MyMemoryTranslationProvider(HttpClient httpClient, IOptions<TranslationProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ProviderName => "MyMemory";

    public bool IsConfigured =>
        _options.Enabled &&
        IsSelectedProvider() &&
        !string.IsNullOrWhiteSpace(_options.MyMemoryEndpoint);

    public async Task<TranslationProviderResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("MyMemory Translation is not configured.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationProviderResult { Text = string.Empty, Provider = ProviderName };
        }

        var source = NormalizeLanguage(sourceLanguage);
        var target = NormalizeLanguage(targetLanguage);
        var translatedSegments = new List<string>();
        foreach (var segment in SplitTextByBytes(text, MaxSegmentBytes))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                translatedSegments.Add(segment);
                continue;
            }

            translatedSegments.Add(await TranslateSegmentAsync(segment, source, target, cancellationToken));
        }

        return new TranslationProviderResult
        {
            Text = string.Join(string.Empty, translatedSegments),
            Provider = ProviderName,
        };
    }

    private async Task<string> TranslateSegmentAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var endpoint = _options.MyMemoryEndpoint.TrimEnd('?', '&');
        var query = new List<string>
        {
            $"q={Uri.EscapeDataString(text)}",
            $"langpair={Uri.EscapeDataString($"{sourceLanguage}|{targetLanguage}")}",
            "mt=1",
        };

        if (!string.IsNullOrWhiteSpace(_options.MyMemoryEmail))
        {
            query.Add($"de={Uri.EscapeDataString(_options.MyMemoryEmail)}");
        }

        if (!string.IsNullOrWhiteSpace(_options.MyMemoryApiKey))
        {
            query.Add($"key={Uri.EscapeDataString(_options.MyMemoryApiKey)}");
        }

        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        using var response = await _httpClient.GetAsync($"{endpoint}{separator}{string.Join("&", query)}", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<MyMemoryResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || result?.ResponseData?.TranslatedText == null)
        {
            var detail = result?.ResponseDetails ?? response.ReasonPhrase ?? "Unknown MyMemory translation error";
            throw new InvalidOperationException($"MyMemory Translation failed: {detail}");
        }

        if (result.ResponseStatus is >= 400)
        {
            var detail = string.IsNullOrWhiteSpace(result.ResponseDetails)
                ? $"HTTP-like response status {result.ResponseStatus}"
                : result.ResponseDetails;
            throw new InvalidOperationException($"MyMemory Translation failed: {detail}");
        }

        return result.ResponseData.TranslatedText;
    }

    private static IReadOnlyList<string> SplitTextByBytes(string text, int maxBytes)
    {
        var parts = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var length = 0;
            var lastGoodLength = 0;
            var lastBreakLength = 0;
            while (start + length < text.Length)
            {
                var candidateLength = length + 1;
                if (char.IsHighSurrogate(text[start + length]) &&
                    start + length + 1 < text.Length &&
                    char.IsLowSurrogate(text[start + length + 1]))
                {
                    candidateLength++;
                }

                var candidate = text.Substring(start, candidateLength);
                if (Encoding.UTF8.GetByteCount(candidate) > maxBytes)
                {
                    break;
                }

                lastGoodLength = candidateLength;
                var current = text[start + candidateLength - 1];
                if (char.IsWhiteSpace(current) || current is '.' or ',' or ';' or ':' or '!' or '?')
                {
                    lastBreakLength = candidateLength;
                }

                length = candidateLength;
            }

            if (lastGoodLength == 0)
            {
                lastGoodLength = 1;
            }

            var take = lastBreakLength > 0 && start + lastGoodLength < text.Length
                ? lastBreakLength
                : lastGoodLength;
            parts.Add(text.Substring(start, take));
            start += take;
        }

        return parts;
    }

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

    private sealed class MyMemoryResponse
    {
        [JsonPropertyName("responseData")]
        public MyMemoryResponseData? ResponseData { get; set; }

        [JsonPropertyName("responseStatus")]
        public int ResponseStatus { get; set; }

        [JsonPropertyName("responseDetails")]
        public string? ResponseDetails { get; set; }
    }

    private sealed class MyMemoryResponseData
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}
