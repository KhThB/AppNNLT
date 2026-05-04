using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public sealed class PayOsPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly PaymentOptions _options;

    public PayOsPaymentGateway(HttpClient httpClient, IOptions<PaymentOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ProviderName => "PayOS";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.PayOS.ClientId) &&
        !string.IsNullOrWhiteSpace(_options.PayOS.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.PayOS.ChecksumKey) &&
        !string.IsNullOrWhiteSpace(_options.ReturnUrl) &&
        !string.IsNullOrWhiteSpace(_options.CancelUrl);

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(BillingRecord record, POI poi, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("PayOS chua duoc cau hinh ClientId/ApiKey/ChecksumKey.");
        }

        var orderCode = record.PaymentOrderCode ?? throw new InvalidOperationException("Billing record is missing order code.");
        var amount = Convert.ToInt32(record.Amount, CultureInfo.InvariantCulture);
        var description = record.PackageName.Equals(SubscriptionPackages.Boost, StringComparison.OrdinalIgnoreCase)
            ? "BOOST"
            : "PREMIUM";
        var returnUrl = _options.ReturnUrl;
        var cancelUrl = _options.CancelUrl;
        var signature = Sign(new SortedDictionary<string, object?>
        {
            ["amount"] = amount,
            ["cancelUrl"] = cancelUrl,
            ["description"] = description,
            ["orderCode"] = orderCode,
            ["returnUrl"] = returnUrl,
        });

        var payload = new
        {
            orderCode,
            amount,
            description,
            buyerName = poi.Name,
            items = new[]
            {
                new { name = $"TourGuide {record.PackageName}", quantity = 1, price = amount },
            },
            cancelUrl,
            returnUrl,
            signature,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.PayOS.BaseUrl.TrimEnd('/')}/v2/payment-requests")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.TryAddWithoutValidation("x-client-id", _options.PayOS.ClientId);
        request.Headers.TryAddWithoutValidation("x-api-key", _options.PayOS.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.PayOS.PartnerCode))
        {
            request.Headers.TryAddWithoutValidation("x-partner-code", _options.PayOS.PartnerCode);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<PayOsCreatePaymentResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || result?.Code != "00" || result.Data == null)
        {
            throw new InvalidOperationException($"PayOS create payment failed: {result?.Desc ?? response.ReasonPhrase}");
        }

        return new PaymentCheckoutResult
        {
            CheckoutUrl = result.Data.CheckoutUrl,
            ProviderPaymentId = result.Data.PaymentLinkId,
            QrCode = result.Data.QrCode,
        };
    }

    public bool VerifyPayOsWebhookSignature(IReadOnlyDictionary<string, object?> data, string signature)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in data)
        {
            sorted[pair.Key] = pair.Value;
        }

        var expected = Sign(sorted);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(signature);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private string Sign(SortedDictionary<string, object?> data)
    {
        var source = string.Join("&", data.Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.PayOS.ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private sealed class PayOsCreatePaymentResponse
    {
        [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
        [JsonPropertyName("desc")] public string Desc { get; set; } = string.Empty;
        [JsonPropertyName("data")] public PayOsCreatePaymentData? Data { get; set; }
    }

    private sealed class PayOsCreatePaymentData
    {
        [JsonPropertyName("paymentLinkId")] public string PaymentLinkId { get; set; } = string.Empty;
        [JsonPropertyName("checkoutUrl")] public string CheckoutUrl { get; set; } = string.Empty;
        [JsonPropertyName("qrCode")] public string QrCode { get; set; } = string.Empty;
    }
}
