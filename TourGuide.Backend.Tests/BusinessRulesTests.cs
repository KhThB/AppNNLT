using TourGuide.API.Services.Implementations;
using TourGuide.Domain.Models;
using Xunit;

namespace TourGuide.Backend.Tests;

public sealed class BusinessRulesTests
{
    [Fact]
    public void ApplyReview_WhenApproved_SetsApprovedWorkflowState()
    {
        var poi = new POI
        {
            Status = PoiWorkflowStatuses.Submitted,
            ApprovalStatus = PoiWorkflowStatuses.Submitted,
        };

        BusinessRules.ApplyReview(poi, true, "admin-1", string.Empty, new DateTime(2026, 4, 26, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PoiWorkflowStatuses.Approved, poi.Status);
        Assert.Equal(PoiWorkflowStatuses.Approved, poi.ApprovalStatus);
        Assert.Equal("admin-1", poi.ReviewedBy);
        Assert.Equal(string.Empty, poi.RejectionReason);
    }

    [Fact]
    public void CalculatePriorityScore_PrefersActiveBoostOverBasePriority()
    {
        var boosted = new POI
        {
            PriorityLevel = 2,
            BoostPriority = 3,
            BoostExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        var normal = new POI
        {
            PriorityLevel = 100,
            BoostPriority = 0,
        };

        var boostedScore = BusinessRules.CalculatePriorityScore(boosted);
        var normalScore = BusinessRules.CalculatePriorityScore(normal);

        Assert.True(boostedScore > normalScore);
    }

    [Theory]
    [InlineData(SubscriptionPackages.Basic, 50)]
    [InlineData(SubscriptionPackages.Premium, 120)]
    [InlineData(SubscriptionPackages.Boost, 200)]
    public void GetEffectiveRadius_UsesPackageDefaults(string packageName, double expectedRadius)
    {
        var poi = new POI { SubscriptionPackage = packageName };

        var radius = BusinessRules.GetEffectiveRadius(poi, new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(expectedRadius, radius);
    }

    [Fact]
    public void GetEffectiveRadius_UsesBoostRadiusOnlyWhenBoostIsActive()
    {
        var now = new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc);
        var poi = new POI
        {
            SubscriptionPackage = SubscriptionPackages.Premium,
            BoostPriority = 1,
            BoostExpiresAt = now.AddMinutes(5),
        };

        Assert.Equal(200, BusinessRules.GetEffectiveRadius(poi, now));

        poi.BoostExpiresAt = now.AddMinutes(-1);
        Assert.Equal(120, BusinessRules.GetEffectiveRadius(poi, now));
    }

    [Fact]
    public void ShouldCountQrScan_BlocksReplayInsideSixHours()
    {
        var now = new DateTime(2026, 4, 26, 18, 0, 0, DateTimeKind.Utc);
        var lastCounted = now.AddHours(-5).AddMinutes(-59);

        Assert.False(BusinessRules.ShouldCountQrScan(lastCounted, now));
        Assert.True(BusinessRules.ShouldCountQrScan(now.AddHours(-6), now));
    }

    [Fact]
    public void ShouldCountNarration_AllowsFourStartsPerMinuteOnly()
    {
        Assert.True(BusinessRules.ShouldCountNarration(0));
        Assert.True(BusinessRules.ShouldCountNarration(3));
        Assert.False(BusinessRules.ShouldCountNarration(4));
    }

    [Fact]
    public void ResolveTranslationText_FallsBackToSourceWhenLegacyMissing()
    {
        var resolved = BusinessRules.ResolveTranslationText(string.Empty, "Xin chao");
        Assert.Equal("Xin chao", resolved.Text);
        Assert.Equal(TranslationStatuses.PendingManual, resolved.Status);

        var manual = BusinessRules.ResolveTranslationText("Hello", "Xin chao");
        Assert.Equal("Hello", manual.Text);
        Assert.Equal(TranslationStatuses.Ready, manual.Status);
    }

    [Fact]
    public void ApplyProvidedTranslations_NormalizesLanguageKeysAndUpdatesPoiDescriptions()
    {
        var poi = new POI();

        var updated = BusinessRules.ApplyProvidedTranslations(poi, new Dictionary<string, string>
        {
            ["en"] = "Hello",
            ["zh-CN"] = "Ni hao",
            ["vi"] = "Ignored source",
            ["ja"] = "   ",
        });

        Assert.Equal(2, updated);
        Assert.Equal("Hello", poi.Description_EN);
        Assert.Equal("Ni hao", poi.Description_ZH);
        Assert.Equal(string.Empty, poi.Description_JA);
        Assert.Equal(string.Empty, poi.Description_VI);
    }

    [Fact]
    public void BuildPoiApprovalChanges_ForNewPoiMarksFieldsAsAdded()
    {
        var poi = new POI
        {
            Name = "Quán mới",
            Address = "Vĩnh Khánh",
            Tags = ["oc dem"],
            SourceDescription = "Nội dung",
            SubscriptionPackage = SubscriptionPackages.Basic,
            Location = new GeoLocation { Coordinates = [106.7, 10.76] },
        };

        var changes = BusinessRules.BuildPoiApprovalChanges(null, poi);

        Assert.Contains(changes, x => x.Field == "Name" && x.ChangeType == "Added" && x.NewValue == "Quán mới");
        Assert.Contains(changes, x => x.Field == "Location" && x.ChangeType == "Added");
    }

    [Fact]
    public void BuildPoiApprovalChanges_ForApprovedSnapshotReturnsModifiedFieldsOnly()
    {
        var approvedAt = new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc);
        var snapshot = new PoiReviewSnapshot
        {
            Name = "Tên cũ",
            Address = "Địa chỉ cũ",
            Tags = ["an vat"],
            SourceDescription = "Cũ",
            SubscriptionPackage = SubscriptionPackages.Basic,
            Radius = 50,
            Latitude = 10.76,
            Longitude = 106.7,
            ApprovedAt = approvedAt,
        };
        var poi = new POI
        {
            Name = "Tên mới",
            Address = "Địa chỉ cũ",
            Tags = ["an vat"],
            SourceDescription = "Cũ",
            SubscriptionPackage = SubscriptionPackages.Premium,
            Location = new GeoLocation { Coordinates = [106.7, 10.76] },
        };

        var changes = BusinessRules.BuildPoiApprovalChanges(snapshot, poi);

        Assert.Contains(changes, x => x.Field == "Name" && x.ChangeType == "Modified");
        Assert.Contains(changes, x => x.Field == "SubscriptionPackage" && x.OldValue == SubscriptionPackages.Basic && x.NewValue == SubscriptionPackages.Premium);
        Assert.DoesNotContain(changes, x => x.Field == "Address");
    }

    [Fact]
    public void NormalizeTag_RemovesVietnameseDiacriticsIncludingDStroke()
    {
        var tag = BusinessRules.NormalizeTag("Ốc Đêm");

        Assert.Equal("oc dem", tag);
    }

    [Fact]
    public void ResolveSubmittedTags_InfersFoodTagsWhenMerchantLeavesTagsEmpty()
    {
        var tags = BusinessRules.ResolveSubmittedTags(Array.Empty<string>(), "Quán Ốc Giàu Tên", "Hải sản đêm Vĩnh Khánh");

        Assert.Contains("oc dem", tags);
    }

    [Fact]
    public void ResolveAccessibleOwnerId_ForMerchantAlwaysUsesCurrentUser()
    {
        var ownerId = BusinessRules.ResolveAccessibleOwnerId("other-owner", KnownRoles.Merchant, "merchant-1");

        Assert.Equal("merchant-1", ownerId);
    }

    [Fact]
    public void ResolveAccessibleOwnerId_ForAdminUsesRequestedOwner()
    {
        var ownerId = BusinessRules.ResolveAccessibleOwnerId("owner-1", KnownRoles.Admin, "admin-1");

        Assert.Equal("owner-1", ownerId);
    }
}
