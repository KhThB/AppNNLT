using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using TourGuide.API.Contracts;
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

    public static double GetDefaultRadiusForPackage(string packageName)
    {
        return packageName switch
        {
            SubscriptionPackages.Premium => 120,
            SubscriptionPackages.Boost => 200,
            _ => 50,
        };
    }

    public static bool IsBoostActive(POI poi, DateTime nowUtc)
    {
        return poi.BoostPriority > 0 &&
               poi.BoostExpiresAt.HasValue &&
               poi.BoostExpiresAt.Value > nowUtc;
    }

    public static double GetEffectiveRadius(POI poi, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (IsBoostActive(poi, now) || string.Equals(poi.SubscriptionPackage, SubscriptionPackages.Boost, StringComparison.OrdinalIgnoreCase))
        {
            return GetDefaultRadiusForPackage(SubscriptionPackages.Boost);
        }

        return GetDefaultRadiusForPackage(poi.SubscriptionPackage);
    }

    public static double GetEffectiveRadiusForPackage(string packageName)
    {
        return GetDefaultRadiusForPackage(packageName);
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

    public static int ApplyProvidedTranslations(POI poi, IReadOnlyDictionary<string, string>? translations)
    {
        if (translations == null || translations.Count == 0)
        {
            return 0;
        }

        var updated = 0;
        foreach (var (language, value) in translations)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            switch (NormalizeLanguageCode(language))
            {
                case "EN":
                    poi.Description_EN = text;
                    updated++;
                    break;
                case "KO":
                    poi.Description_KO = text;
                    updated++;
                    break;
                case "JA":
                    poi.Description_JA = text;
                    updated++;
                    break;
                case "ZH":
                    poi.Description_ZH = text;
                    updated++;
                    break;
            }
        }

        return updated;
    }

    public static void ClearTargetTranslations(POI poi)
    {
        poi.Description_EN = string.Empty;
        poi.Description_KO = string.Empty;
        poi.Description_JA = string.Empty;
        poi.Description_ZH = string.Empty;
    }

    public static IReadOnlyDictionary<string, string> GetTargetTranslations(POI poi)
    {
        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddIfNotBlank(translations, "EN", poi.Description_EN);
        AddIfNotBlank(translations, "KO", poi.Description_KO);
        AddIfNotBlank(translations, "JA", poi.Description_JA);
        AddIfNotBlank(translations, "ZH", poi.Description_ZH);
        return translations;
    }

    public static string NormalizeLanguageCode(string? language)
    {
        return (language ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "VI" or "VN" => "VI",
            "EN" => "EN",
            "KO" or "KR" => "KO",
            "JA" or "JP" => "JA",
            "ZH" or "CN" or "ZH-CN" => "ZH",
            var value => value,
        };
    }

    public static PoiReviewSnapshot CreateApprovedSnapshot(POI poi, string? approvedBy, DateTime approvedAt)
    {
        return new PoiReviewSnapshot
        {
            PoiId = poi.Id,
            OwnerId = poi.OwnerId,
            Name = poi.Name,
            Address = poi.Address,
            Tags = NormalizeTags(poi.Tags),
            SourceLanguage = poi.SourceLanguage,
            SourceDescription = ResolveSourceDescription(poi),
            Description_VI = string.IsNullOrWhiteSpace(poi.Description_VI) ? ResolveSourceDescription(poi) : poi.Description_VI,
            Description_EN = poi.Description_EN,
            Description_KO = poi.Description_KO,
            Description_JA = poi.Description_JA,
            Description_ZH = poi.Description_ZH,
            ImageUrl = poi.ImageUrl,
            SubscriptionPackage = poi.SubscriptionPackage,
            Radius = GetEffectiveRadius(poi, approvedAt),
            Latitude = poi.Location.Coordinates.ElementAtOrDefault(1),
            Longitude = poi.Location.Coordinates.ElementAtOrDefault(0),
            ContentVersion = poi.ContentVersion,
            ApprovedBy = approvedBy,
            ApprovedAt = approvedAt,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static IReadOnlyList<PoiChangeItemResponse> BuildPoiApprovalChanges(PoiReviewSnapshot? snapshot, POI poi)
    {
        var changes = new List<PoiChangeItemResponse>();
        var currentRadius = GetEffectiveRadius(poi);

        AddChange(changes, snapshot, "Name", "Tên quán", snapshot?.Name, poi.Name);
        AddChange(changes, snapshot, "Address", "Địa chỉ", snapshot?.Address, poi.Address);
        AddChange(changes, snapshot, "Tags", "Tag", JoinTags(snapshot?.Tags), JoinTags(NormalizeTags(poi.Tags)));
        AddChange(changes, snapshot, "SourceDescription", "Nội dung nguồn", snapshot?.SourceDescription, ResolveSourceDescription(poi));
        AddChange(changes, snapshot, "Description_EN", "Bản dịch EN", snapshot?.Description_EN, poi.Description_EN);
        AddChange(changes, snapshot, "Description_KO", "Bản dịch KO", snapshot?.Description_KO, poi.Description_KO);
        AddChange(changes, snapshot, "Description_JA", "Bản dịch JA", snapshot?.Description_JA, poi.Description_JA);
        AddChange(changes, snapshot, "Description_ZH", "Bản dịch ZH", snapshot?.Description_ZH, poi.Description_ZH);
        AddChange(changes, snapshot, "Location", "Vị trí", FormatLocation(snapshot?.Latitude, snapshot?.Longitude), FormatLocation(poi.Location.Coordinates.ElementAtOrDefault(1), poi.Location.Coordinates.ElementAtOrDefault(0)));
        AddChange(changes, snapshot, "ImageUrl", "Ảnh", snapshot?.ImageUrl, poi.ImageUrl);
        AddChange(changes, snapshot, "SubscriptionPackage", "Gói dịch vụ", snapshot?.SubscriptionPackage, poi.SubscriptionPackage);
        AddChange(changes, snapshot, "Radius", "Radius hệ thống", snapshot?.Radius.ToString("0", CultureInfo.InvariantCulture), currentRadius.ToString("0", CultureInfo.InvariantCulture));

        return changes;
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

    private static void AddChange(
        List<PoiChangeItemResponse> changes,
        PoiReviewSnapshot? snapshot,
        string field,
        string label,
        string? oldValue,
        string? newValue)
    {
        var normalizedOld = NormalizeChangeValue(oldValue);
        var normalizedNew = NormalizeChangeValue(newValue);
        if (snapshot != null && string.Equals(normalizedOld, normalizedNew, StringComparison.Ordinal))
        {
            return;
        }

        var changeType = snapshot == null
            ? "Added"
            : string.IsNullOrWhiteSpace(normalizedNew)
                ? "Removed"
                : string.IsNullOrWhiteSpace(normalizedOld)
                    ? "Added"
                    : "Modified";

        changes.Add(new PoiChangeItemResponse
        {
            Field = field,
            Label = label,
            ChangeType = changeType,
            OldValue = snapshot == null ? string.Empty : normalizedOld,
            NewValue = normalizedNew,
        });
    }

    private static string ResolveSourceDescription(POI poi)
    {
        return string.IsNullOrWhiteSpace(poi.SourceDescription) ? poi.Description_VI : poi.SourceDescription;
    }

    private static string NormalizeChangeValue(string? value)
    {
        return string.Join(" ", (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string JoinTags(IEnumerable<string>? tags)
    {
        return string.Join(", ", NormalizeTags(tags));
    }

    private static string FormatLocation(double? latitude, double? longitude)
    {
        var lat = latitude ?? 0;
        var lng = longitude ?? 0;
        if (lat == 0 && lng == 0)
        {
            return string.Empty;
        }

        return FormattableString.Invariant($"{lat:0.000000}, {lng:0.000000}");
    }

    public static string NormalizeTag(string tag)
    {
        var normalized = RemoveDiacritics((tag ?? string.Empty).Trim().ToLowerInvariant())
            .Replace("-", " ")
            .Replace("_", " ");

        return string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        return (tags ?? Array.Empty<string>())
            .Select(NormalizeTag)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    public static List<string> ResolveSubmittedTags(IEnumerable<string>? tags, string name, string sourceDescription)
    {
        var normalized = NormalizeTags(tags);
        return normalized.Count > 0 ? normalized : InferTags(name, sourceDescription).ToList();
    }

    public static IReadOnlyList<string> InferTags(string name, string sourceDescription)
    {
        var text = NormalizeTag($"{name} {sourceDescription}");
        var tags = new List<string>();

        AddIfContains(tags, text, "oc dem", "oc", "ngheu", "so", "hau", "hai san");
        AddIfContains(tags, text, "an vat", "banh trang", "xien", "chien", "goi cuon", "pha lau");
        AddIfContains(tags, text, "tra sua", "milk tea", "matcha");
        AddIfContains(tags, text, "nhau", "bia", "lau", "nuong");
        AddIfContains(tags, text, "com bun", "com", "bun", "pho", "mi", "hu tieu");

        return tags.Count == 0 ? new[] { "an vat" } : tags;
    }

    private static void AddIfContains(List<string> tags, string text, string tag, params string[] keywords)
    {
        if (keywords.Any(text.Contains) && !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(tag);
        }
    }

    private static void AddIfNotBlank(Dictionary<string, string> translations, string language, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            translations[language] = text;
        }
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c == 'đ')
            {
                builder.Append('d');
                continue;
            }

            if (c == 'Đ')
            {
                builder.Append('D');
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
