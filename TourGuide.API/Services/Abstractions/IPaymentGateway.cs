using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Abstractions;

public interface IPaymentGateway
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<PaymentCheckoutResult> CreateCheckoutAsync(BillingRecord record, POI poi, CancellationToken cancellationToken = default);
    bool VerifyPayOsWebhookSignature(IReadOnlyDictionary<string, object?> data, string signature);
}

public sealed class PaymentCheckoutResult
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string ProviderPaymentId { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
}
