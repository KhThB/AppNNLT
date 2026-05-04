namespace TourGuide.API.Infrastructure.Options;

public sealed class PaymentOptions
{
    public string Provider { get; set; } = "PayOS";
    public decimal PremiumMonthlyAmount { get; set; } = 150_000m;
    public decimal BoostMonthlyAmount { get; set; } = 50_000m;
    public int PremiumDurationDays { get; set; } = 30;
    public int BoostDurationDays { get; set; } = 30;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public PayOsOptions PayOS { get; set; } = new();
}

public sealed class PayOsOptions
{
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string PartnerCode { get; set; } = string.Empty;
}
