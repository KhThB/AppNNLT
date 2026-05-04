using System.Security.Claims;
using MongoDB.Driver;
using TourGuide.API.Contracts;
using TourGuide.API.Infrastructure.Mongo;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;
using Microsoft.Extensions.Options;

namespace TourGuide.API.Services.Implementations;

public sealed class BillingService : IBillingService
{
    private readonly MongoCollections _collections;
    private readonly IPaymentGateway _paymentGateway;
    private readonly PaymentOptions _options;
    private readonly IAuditService _auditService;

    public BillingService(
        MongoCollections collections,
        IPaymentGateway paymentGateway,
        IOptions<PaymentOptions> options,
        IAuditService auditService)
    {
        _collections = collections;
        _paymentGateway = paymentGateway;
        _options = options.Value;
        _auditService = auditService;
    }

    public async Task<PremiumCheckoutResponse> CreatePremiumCheckoutAsync(PremiumCheckoutRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        return await CreatePackageCheckoutAsync(
            new PackageCheckoutRequest
            {
                PoiId = request.PoiId,
                PackageName = SubscriptionPackages.Premium,
            },
            principal,
            cancellationToken);
    }

    public async Task<PremiumCheckoutResponse> CreatePackageCheckoutAsync(PackageCheckoutRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var poi = await _collections.Pois.Find(x => x.Id == request.PoiId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Khong tim thay POI.");

        var role = principal.FindFirstValue(ClaimTypes.Role);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (role != KnownRoles.Admin && poi.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("Ban khong co quyen thanh toan POI nay.");
        }

        var packageName = NormalizePackage(request.PackageName);
        if (!_paymentGateway.IsConfigured)
        {
            throw new InvalidOperationException("PayOS chưa được cấu hình. Vui lòng kiểm tra Payment:PayOS và ReturnUrl/CancelUrl.");
        }

        if (packageName == SubscriptionPackages.Premium &&
            poi.SubscriptionPackage == SubscriptionPackages.Premium &&
            poi.SubscriptionExpiry.HasValue &&
            poi.SubscriptionExpiry.Value > DateTime.UtcNow)
        {
            throw new InvalidOperationException("POI nay dang o goi Premium con hieu luc.");
        }

        if (packageName == SubscriptionPackages.Boost &&
            poi.BoostExpiresAt.HasValue &&
            poi.BoostExpiresAt.Value > DateTime.UtcNow)
        {
            throw new InvalidOperationException("POI nay dang co Boost con hieu luc.");
        }

        var now = DateTime.UtcNow;
        var durationDays = packageName == SubscriptionPackages.Boost ? _options.BoostDurationDays : _options.PremiumDurationDays;
        var record = new BillingRecord
        {
            OwnerId = poi.OwnerId,
            PoiId = poi.Id,
            BillingType = packageName == SubscriptionPackages.Boost ? "Boost" : "Subscription",
            PackageName = packageName,
            Amount = packageName == SubscriptionPackages.Boost ? _options.BoostMonthlyAmount : _options.PremiumMonthlyAmount,
            Currency = "VND",
            Status = BillingStatuses.PendingPayment,
            Provider = _paymentGateway.ProviderName,
            PaymentOrderCode = BuildOrderCode(now),
            EffectiveFrom = now,
            EffectiveTo = now.AddDays(durationDays),
            AutoRenew = false,
            Notes = $"{packageName} checkout created",
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _collections.BillingRecords.InsertOneAsync(record, cancellationToken: cancellationToken);

        var checkout = await _paymentGateway.CreateCheckoutAsync(record, poi, cancellationToken);
        var update = Builders<BillingRecord>.Update
            .Set(x => x.ProviderPaymentId, checkout.ProviderPaymentId)
            .Set(x => x.CheckoutUrl, checkout.CheckoutUrl)
            .Set(x => x.QrCode, checkout.QrCode)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await _collections.BillingRecords.UpdateOneAsync(x => x.Id == record.Id, update, cancellationToken: cancellationToken);
        record.ProviderPaymentId = checkout.ProviderPaymentId;
        record.CheckoutUrl = checkout.CheckoutUrl;
        record.QrCode = checkout.QrCode;

        await _auditService.WriteAsync(
            "BILLING_CHECKOUT_CREATED",
            "BillingRecord",
            record.Id,
            new { record.PoiId, record.Provider, record.Amount, record.PaymentOrderCode },
            userId,
            role,
            cancellationToken);

        return new PremiumCheckoutResponse
        {
            BillingRecordId = record.Id,
            Provider = record.Provider,
            OrderCode = record.PaymentOrderCode ?? 0,
            Amount = record.Amount,
            Currency = record.Currency,
            Status = record.Status,
            CheckoutUrl = record.CheckoutUrl,
            PaymentLinkId = record.ProviderPaymentId,
            QrCode = record.QrCode,
        };
    }

    public async Task<IReadOnlyList<BillingRecordResponse>> GetOwnerBillingAsync(string ownerId, ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var resolvedOwnerId = BusinessRules.ResolveAccessibleOwnerId(
            ownerId,
            principal.FindFirstValue(ClaimTypes.Role),
            principal.FindFirstValue(ClaimTypes.NameIdentifier));

        var records = await _collections.BillingRecords.Find(x => x.OwnerId == resolvedOwnerId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<BillingRecordResponse>> GetAdminBillingAsync(CancellationToken cancellationToken = default)
    {
        var records = await _collections.BillingRecords.Find(FilterDefinition<BillingRecord>.Empty)
            .SortByDescending(x => x.CreatedAt)
            .Limit(200)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public BillingConfigStatusResponse GetConfigStatus()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.PayOS.ClientId)) missing.Add("Payment:PayOS:ClientId");
        if (string.IsNullOrWhiteSpace(_options.PayOS.ApiKey)) missing.Add("Payment:PayOS:ApiKey");
        if (string.IsNullOrWhiteSpace(_options.PayOS.ChecksumKey)) missing.Add("Payment:PayOS:ChecksumKey");
        if (string.IsNullOrWhiteSpace(_options.ReturnUrl)) missing.Add("Payment:ReturnUrl");
        if (string.IsNullOrWhiteSpace(_options.CancelUrl)) missing.Add("Payment:CancelUrl");

        return new BillingConfigStatusResponse
        {
            Provider = _paymentGateway.ProviderName,
            IsConfigured = _paymentGateway.IsConfigured,
            MissingFields = missing,
            PremiumMonthlyAmount = _options.PremiumMonthlyAmount,
            BoostMonthlyAmount = _options.BoostMonthlyAmount,
            PremiumDurationDays = _options.PremiumDurationDays,
            BoostDurationDays = _options.BoostDurationDays,
        };
    }

    public async Task ProcessPayOsWebhookAsync(PayOsWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var signatureData = ToSignatureData(request.Data);
        if (!_paymentGateway.VerifyPayOsWebhookSignature(signatureData, request.Signature))
        {
            throw new ArgumentException("Chu ky webhook PayOS khong hop le.");
        }

        var record = await _collections.BillingRecords.Find(x =>
                x.PaymentOrderCode == request.Data.OrderCode ||
                x.ProviderPaymentId == request.Data.PaymentLinkId)
            .FirstOrDefaultAsync(cancellationToken);

        if (record == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (request.Success && request.Code == "00" && request.Data.Code == "00")
        {
            record.Status = BillingStatuses.Active;
            record.ProviderTransactionId = request.Data.Reference;
            record.ProviderPaymentId = request.Data.PaymentLinkId;
            record.PaidAt = now;
            record.EffectiveFrom = now;
            var durationDays = record.PackageName == SubscriptionPackages.Boost ? _options.BoostDurationDays : _options.PremiumDurationDays;
            record.EffectiveTo = now.AddDays(durationDays);
            record.UpdatedAt = now;

            await _collections.BillingRecords.ReplaceOneAsync(x => x.Id == record.Id, record, cancellationToken: cancellationToken);

            var poi = await _collections.Pois.Find(x => x.Id == record.PoiId).FirstOrDefaultAsync(cancellationToken);
            var poiUpdate = Builders<POI>.Update
                .Set(x => x.IsPaid, true)
                .Set(x => x.LastTransactionId, record.ProviderTransactionId)
                .Set(x => x.UpdatedAt, now);

            if (record.PackageName == SubscriptionPackages.Boost)
            {
                var radius = BusinessRules.GetDefaultRadiusForPackage(SubscriptionPackages.Boost);
                poiUpdate = poiUpdate
                    .Set(x => x.BoostPriority, Math.Max(poi?.BoostPriority ?? 0, 1))
                    .Set(x => x.BoostExpiresAt, record.EffectiveTo)
                    .Set(x => x.Radius, radius);
            }
            else
            {
                var radius = BusinessRules.GetDefaultRadiusForPackage(SubscriptionPackages.Premium);
                poiUpdate = poiUpdate
                    .Set(x => x.SubscriptionPackage, SubscriptionPackages.Premium)
                    .Set(x => x.SubscriptionExpiry, record.EffectiveTo)
                    .Set(x => x.Radius, radius);
            }

            await _collections.Pois.UpdateOneAsync(x => x.Id == record.PoiId, poiUpdate, cancellationToken: cancellationToken);

            await _auditService.WriteAsync(
                "BILLING_PAYMENT_COMPLETED",
                "BillingRecord",
                record.Id,
                new { record.PoiId, record.Amount, record.ProviderTransactionId },
                record.OwnerId,
                KnownRoles.Merchant,
                cancellationToken);

            return;
        }

        record.Status = BillingStatuses.Failed;
        record.FailureReason = request.Desc;
        record.UpdatedAt = now;
        await _collections.BillingRecords.ReplaceOneAsync(x => x.Id == record.Id, record, cancellationToken: cancellationToken);
    }

    private static string NormalizePackage(string packageName)
    {
        return string.Equals(packageName, SubscriptionPackages.Boost, StringComparison.OrdinalIgnoreCase)
            ? SubscriptionPackages.Boost
            : SubscriptionPackages.Premium;
    }

    private static long BuildOrderCode(DateTime now)
    {
        return long.Parse($"{now:yyMMddHHmmss}{Random.Shared.Next(10, 99)}");
    }

    private static IReadOnlyDictionary<string, object?> ToSignatureData(PayOsWebhookData data)
    {
        return new SortedDictionary<string, object?>
        {
            ["accountNumber"] = data.AccountNumber,
            ["amount"] = data.Amount,
            ["code"] = data.Code,
            ["counterAccountBankId"] = data.CounterAccountBankId,
            ["counterAccountBankName"] = data.CounterAccountBankName,
            ["counterAccountName"] = data.CounterAccountName,
            ["counterAccountNumber"] = data.CounterAccountNumber,
            ["currency"] = data.Currency,
            ["desc"] = data.Desc,
            ["description"] = data.Description,
            ["orderCode"] = data.OrderCode,
            ["paymentLinkId"] = data.PaymentLinkId,
            ["reference"] = data.Reference,
            ["transactionDateTime"] = data.TransactionDateTime,
            ["virtualAccountName"] = data.VirtualAccountName,
            ["virtualAccountNumber"] = data.VirtualAccountNumber,
        };
    }

    private static BillingRecordResponse Map(BillingRecord record)
    {
        return new BillingRecordResponse
        {
            Id = record.Id,
            OwnerId = record.OwnerId,
            PoiId = record.PoiId,
            Provider = record.Provider,
            BillingType = record.BillingType,
            PackageName = record.PackageName,
            Amount = record.Amount,
            Currency = record.Currency,
            Status = record.Status,
            CheckoutUrl = record.CheckoutUrl,
            PaymentOrderCode = record.PaymentOrderCode,
            PaidAt = record.PaidAt,
            EffectiveTo = record.EffectiveTo,
        };
    }
}
