using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourGuide.API.Contracts;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api/poi")]
public sealed class POIController : ControllerBase
{
    private readonly IPoiService _poiService;
    private readonly IImageStorageService _imageStorageService;

    public POIController(IPoiService poiService, IImageStorageService imageStorageService)
    {
        _poiService = poiService;
        _imageStorageService = imageStorageService;
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPost]
    public async Task<ActionResult<PoiListItemResponse>> Create([FromBody] PoiCreateRequest request, CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return Ok(await _poiService.CreateAsync(ownerId, request, cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PoiListItemResponse>>> Query(
        [FromQuery] string search = "",
        [FromQuery] string status = "",
        [FromQuery] string tag = "",
        [FromQuery] string ownerId = "",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _poiService.QueryAsync(new PoiQueryRequest
        {
            Search = search,
            Status = status,
            Tag = tag,
            OwnerId = ownerId,
            Page = page,
            PageSize = pageSize,
        }, cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpGet("map-summary")]
    public async Task<ActionResult<IReadOnlyList<PoiMapPointResponse>>> MapSummary(CancellationToken cancellationToken)
    {
        return Ok(await _poiService.GetMapSummaryAsync(cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpGet("submitted-with-changes")]
    public async Task<ActionResult<IReadOnlyList<PoiApprovalItemResponse>>> SubmittedWithChanges(CancellationToken cancellationToken)
    {
        return Ok(await _poiService.GetSubmittedWithChangesAsync(cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpGet("owner/{ownerId}")]
    public async Task<ActionResult<IReadOnlyList<PoiListItemResponse>>> GetByOwner(string ownerId, CancellationToken cancellationToken)
    {
        return Ok(await _poiService.GetOwnerPoisAsync(ownerId, User, cancellationToken));
    }

    [HttpGet("approved")]
    public async Task<ActionResult<PagedResponse<PoiListItemResponse>>> GetApproved(
        [FromQuery] string tag = "",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _poiService.QueryAsync(new PoiQueryRequest
        {
            Status = PoiWorkflowStatuses.Approved,
            Tag = tag,
            Page = page,
            PageSize = pageSize,
        }, cancellationToken));
    }

    [HttpGet("details/{id}")]
    public async Task<ActionResult<PoiPublicDetailResponse>> GetDetails(string id, CancellationToken cancellationToken)
    {
        var result = await _poiService.GetPublicDetailAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<IReadOnlyList<PoiListItemResponse>>> GetNearby(
        [FromQuery] double longitude,
        [FromQuery] double latitude,
        [FromQuery] double maxDistance = 5000,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _poiService.GetNearbyAsync(new NearbyPoiQueryRequest
        {
            Longitude = longitude,
            Latitude = latitude,
            MaxDistance = maxDistance,
        }, cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPut("{id}")]
    public async Task<ActionResult<PoiListItemResponse>> Update(string id, [FromBody] PoiUpdateRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _poiService.UpdateAsync(id, User, request, cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _poiService.ArchiveAsync(id, User, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<PoiListItemResponse>> Submit(string id, CancellationToken cancellationToken)
    {
        return Ok(await _poiService.SubmitAsync(id, User, cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpPost("{id}/review")]
    public async Task<ActionResult<PoiListItemResponse>> Review(string id, [FromBody] PoiReviewRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _poiService.ReviewAsync(id, User, request, cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpPut("{id}/approve")]
    public async Task<ActionResult<PoiListItemResponse>> Approve(string id, CancellationToken cancellationToken)
    {
        return Ok(await _poiService.ReviewAsync(id, User, new PoiReviewRequest { Approve = true }, cancellationToken));
    }

    [Authorize(Roles = KnownRoles.Admin)]
    [HttpPut("{id}/reject")]
    public async Task<ActionResult<PoiListItemResponse>> Reject(string id, [FromBody] PoiReviewRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _poiService.ReviewAsync(id, User, new PoiReviewRequest
        {
            Approve = false,
            RejectionReason = request.RejectionReason,
        }, cancellationToken));
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPost("upload-image")]
    public async Task<ActionResult<object>> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        var imageUrl = await _imageStorageService.UploadPoiImageAsync(file, cancellationToken);
        return Ok(new { imageUrl });
    }
}
