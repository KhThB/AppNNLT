namespace TourGuide.API.Contracts;

public sealed class PoiCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "vi";
    public string SourceDescription { get; set; } = string.Empty;
    public string MerchantNote { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 50;
    public string SubscriptionPackage { get; set; } = "Basic";
}

public sealed class PoiUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "vi";
    public string SourceDescription { get; set; } = string.Empty;
    public string MerchantNote { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 50;
    public int PriorityLevel { get; set; }
    public int BoostPriority { get; set; }
    public string SubscriptionPackage { get; set; } = "Basic";
    public string ImageUrl { get; set; } = string.Empty;
}

public sealed class PoiReviewRequest
{
    public bool Approve { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}

public sealed class PoiQueryRequest
{
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class NearbyPoiQueryRequest
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public double MaxDistance { get; set; } = 5000;
}

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalItems { get; set; }
    public int TotalPages { get; set; }
}

public sealed class PoiListItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public string TranslationStatus { get; set; } = string.Empty;
    public string SubscriptionPackage { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double Radius { get; set; }
    public int PriorityLevel { get; set; }
    public int BoostPriority { get; set; }
    public int CountedQrScanCount { get; set; }
    public int CountedTtsPlayCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string SourceDescription { get; set; } = string.Empty;
}

public sealed class PoiLocalizedContent
{
    public string Description { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class PoiPublicDetailResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public IReadOnlyDictionary<string, PoiLocalizedContent> Contents { get; set; } =
        new Dictionary<string, PoiLocalizedContent>();
}
