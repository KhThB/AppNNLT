using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Implementations;
using TourGuide.Domain.Models;
using Xunit;

namespace TourGuide.Backend.Tests;

public sealed class PaymentGatewayTests
{
    [Fact]
    public void PayOsPaymentGateway_IsConfigured_RequiresCredentialsAndReturnUrls()
    {
        var missingUrls = CreateGateway(new PaymentOptions
        {
            PayOS = new PayOsOptions
            {
                ClientId = "client",
                ApiKey = "api",
                ChecksumKey = "checksum",
            },
        });

        var configured = CreateGateway(new PaymentOptions
        {
            ReturnUrl = "https://example.test/success",
            CancelUrl = "https://example.test/cancel",
            PayOS = new PayOsOptions
            {
                ClientId = "client",
                ApiKey = "api",
                ChecksumKey = "checksum",
            },
        });

        Assert.False(missingUrls.IsConfigured);
        Assert.True(configured.IsConfigured);
    }

    [Theory]
    [InlineData(SubscriptionPackages.Basic, 50)]
    [InlineData(SubscriptionPackages.Premium, 120)]
    [InlineData(SubscriptionPackages.Boost, 200)]
    public void GetDefaultRadiusForPackage_UsesPlanMinimums(string packageName, double expectedRadius)
    {
        Assert.Equal(expectedRadius, BusinessRules.GetDefaultRadiusForPackage(packageName));
    }

    private static PayOsPaymentGateway CreateGateway(PaymentOptions options)
    {
        return new PayOsPaymentGateway(new HttpClient(), Options.Create(options));
    }
}
