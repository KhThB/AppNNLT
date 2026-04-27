using System.Security.Cryptography;
using System.Text;
using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Implementations;

public static class BusinessRules
{
    public static POI ApplyReview(POI poi, bool approve, string reviewerId, string rejectionReason, DateTime nowUtc)
    {
        poi.ApprovalStatus = approve ? PoiWorkflowStatuses.Approved : PoiWorkflowStatuses.Rejected;
        poi.Status = poi.ApprovalStatus;
        poi.ReviewedBy = reviewerId;
        poi.ReviewedAt = nowUtc;
        poi.RejectionReason = approve ? string.Empty : rejectionReason;
        poi.UpdatedAt = nowUtc;
        return poi;
    }

    public static int CalculatePriorityScore(POI poi)
    {
        var boostActive = poi.BoostExpiresAt.HasValue && poi.BoostExpiresAt > DateTime.UtcNow ? poi.BoostPriority : 0;
        return (boostActive * 1000) + poi.PriorityLevel;
    }

    public static string ResolveAccessibleOwnerId(string requestedOwnerId, string? role, string? userId)
    {
        if (role == KnownRoles.Admin)
        {
            if (string.IsNullOrWhiteSpace(requestedOwnerId))
            {
                throw new ArgumentException("Missing ownerId.");
            }

            return requestedOwnerId.Trim();
        }

        if (role == KnownRoles.Merchant && !string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Owner data is not accessible for this user.");
    }

    public static bool ShouldCountQrScan(DateTime? lastCountedAt, DateTime nowUtc)
    {
        return lastCountedAt == null || nowUtc - lastCountedAt.Value >= TimeSpan.FromHours(6);
    }

    public static DateTime GetQrCooldownEnd(DateTime nowUtc) => nowUtc.AddHours(6);

    public static bool ShouldCountNarration(int countedEventsInLastMinute)
    {
        return countedEventsInLastMinute < 4;
    }

    public static string BuildMinuteWindowKey(DateTime nowUtc)
    {
        return nowUtc.ToString("yyyyMMddHHmm");
    }

    public static string BuildQrWindowKey(DateTime nowUtc)
    {
        var sixHourBlock = nowUtc.Hour / 6;
        return $"{nowUtc:yyyyMMdd}-{sixHourBlock}";
    }

    public static (string Text, string Status) ResolveTranslationText(string legacyText, string sourceText)
    {
        return !string.IsNullOrWhiteSpace(legacyText)
            ? (legacyText, TranslationStatuses.Ready)
            : (sourceText, TranslationStatuses.PendingManual);
    }

    public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371e3;
        const double radians = Math.PI / 180d;
        var dLat = (lat2 - lat1) * radians;
        var dLon = (lon2 - lon1) * radians;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * radians) * Math.Cos(lat2 * radians) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static string CalculateHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
