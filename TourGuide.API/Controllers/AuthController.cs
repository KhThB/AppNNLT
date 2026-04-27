using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TourGuide.API.Contracts;
using TourGuide.API.Services.Abstractions;
using TourGuide.Domain.Models;

namespace TourGuide.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> AdminLogin([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.LoginAsync(request, KnownRoles.Admin, cancellationToken));
    }

    [HttpPost("merchant/login")]
    public async Task<ActionResult<AuthResponse>> MerchantLogin([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.LoginAsync(request, KnownRoles.Merchant, cancellationToken));
    }

    [HttpPost("merchant/register")]
    public async Task<ActionResult<AuthResponse>> MerchantRegister([FromBody] MerchantRegisterRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.RegisterMerchantAsync(request, cancellationToken));
    }

    [HttpPost("social-login")]
    public async Task<ActionResult<AuthResponse>> SocialLoginContract([FromBody] SocialLoginContract request, CancellationToken cancellationToken)
    {
        return Ok(await _authService.AcceptSocialContractAsync(request, cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(User, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<SessionUserResponse>> Me(CancellationToken cancellationToken)
    {
        var me = await _authService.GetCurrentAsync(User, cancellationToken);
        return me == null ? Unauthorized() : Ok(me);
    }
}
