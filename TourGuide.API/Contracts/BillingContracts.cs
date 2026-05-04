namespace TourGuide.API.Contracts;

public sealed class PremiumCheckoutRequest
{
    public string PoiId { get; set; } = string.Empty;
}

public sealed class PackageCheckoutRequest
{
    public string PoiId { get; set; } = string.Empty;
    public string PackageName { get; set; } = "Premium";
}

public sealed class PremiumCheckoutResponse
{
    public string BillingRecordId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string PaymentLinkId { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
}

public sealed class BillingRecordResponse
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string PoiId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string BillingType { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public long? PaymentOrderCode { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class BillingConfigStatusResponse
{
    public string Provider { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
    public decimal PremiumMonthlyAmount { get; set; }
    public decimal BoostMonthlyAmount { get; set; }
    public int PremiumDurationDays { get; set; }
    public int BoostDurationDays { get; set; }
}

public sealed class PayOsWebhookRequest
{
    public string Code { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public bool Success { get; set; }
    public PayOsWebhookData Data { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
}

public sealed class PayOsWebhookData
{
    public long OrderCode { get; set; }
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string TransactionDateTime { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public string PaymentLinkId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public string CounterAccountBankId { get; set; } = string.Empty;
    public string CounterAccountBankName { get; set; } = string.Empty;
    public string CounterAccountName { get; set; } = string.Empty;
    public string CounterAccountNumber { get; set; } = string.Empty;
    public string VirtualAccountName { get; set; } = string.Empty;
    public string VirtualAccountNumber { get; set; } = string.Empty;
}
