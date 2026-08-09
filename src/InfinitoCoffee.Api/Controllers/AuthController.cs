using InfinitoCoffee.Api.Authentication;
using InfinitoCoffee.Api.Contracts.Authentication;
using InfinitoCoffee.Application.Authentication.Commands;
using InfinitoCoffee.Application.Authentication.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationAuthenticationService = InfinitoCoffee.Application.Authentication.Services.AuthenticationService;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ApplicationAuthenticationService _authenticationService;
    private readonly UserClaimsPrincipalFactory _claimsPrincipalFactory;

    public AuthController(
        ApplicationAuthenticationService authenticationService,
        UserClaimsPrincipalFactory claimsPrincipalFactory)
    {
        _authenticationService = authenticationService;
        _claimsPrincipalFactory = claimsPrincipalFactory;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthenticatedUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var authenticatedUser = await _authenticationService.AuthenticateAsync(
            new LoginCommand(request.Username, request.Password),
            cancellationToken);

        var principal = _claimsPrincipalFactory.Create(authenticatedUser);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false
        };

        await HttpContext.SignInAsync(
            AuthenticationConstants.CookieScheme,
            principal,
            properties);

        return Ok(MapResponse(authenticatedUser));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthenticationConstants.CookieScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthenticatedUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserResponse>> Me()
    {
        if (!_claimsPrincipalFactory.TryRead(User, out var authenticatedUser))
        {
            await HttpContext.SignOutAsync(AuthenticationConstants.CookieScheme);
            return Challenge(AuthenticationConstants.CookieScheme);
        }

        return Ok(MapResponse(authenticatedUser!));
    }

    private static AuthenticatedUserResponse MapResponse(AuthenticatedUserDto user)
    {
        return new AuthenticatedUserResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role.ToString());
    }
}
