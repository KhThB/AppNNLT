using System.Security.Claims;
using TourGuide.API.Contracts;

namespace TourGuide.API.Services.Abstractions;

public interface IPoiService
{
    Task<PoiListItemResponse> CreateAsync(string ownerId, PoiCreateRequest request, CancellationToken cancellationToken = default);
    Task<PoiListItemResponse?> GetByIdAsync(string poiId, CancellationToken cancellationToken = default);
    Task<PoiPublicDetailResponse?> GetPublicDetailAsync(string poiId, CancellationToken cancellationToken = default);
    Task<PagedResponse<PoiListItemResponse>> QueryAsync(PoiQueryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PoiApprovalItemResponse>> GetSubmittedWithChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PoiMapPointResponse>> GetMapSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PoiListItemResponse>> GetOwnerPoisAsync(string ownerId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PoiListItemResponse>> GetNearbyAsync(NearbyPoiQueryRequest request, CancellationToken cancellationToken = default);
    Task<PoiListItemResponse> UpdateAsync(string poiId, ClaimsPrincipal principal, PoiUpdateRequest request, CancellationToken cancellationToken = default);
    Task<PoiListItemResponse> SubmitAsync(string poiId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<PoiListItemResponse> ReviewAsync(string poiId, ClaimsPrincipal principal, PoiReviewRequest request, CancellationToken cancellationToken = default);
    Task<string> UpdateImageAsync(string poiId, ClaimsPrincipal principal, string imageUrl, CancellationToken cancellationToken = default);
    Task ArchiveAsync(string poiId, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<RepairResponse> RepairMissingTagsAsync(CancellationToken cancellationToken = default);
}
