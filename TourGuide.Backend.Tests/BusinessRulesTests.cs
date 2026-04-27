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
