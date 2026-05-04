using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourGuide.API.Contracts;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api/translation")]
public sealed class TranslationController : ControllerBase
{
    private readonly ITranslationService _translationService;

    public TranslationController(ITranslationService translationService)
    {
        _translationService = translationService;
    }

    [Authorize(Roles = $"{KnownRoles.Admin},{KnownRoles.Merchant}")]
    [HttpPost("preview")]
    public async Task<ActionResult<TranslationPreviewResponse>> Preview(
        [FromBody] TranslationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _translationService.PreviewAsync(request, cancellationToken));
    }
}
