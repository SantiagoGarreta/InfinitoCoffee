using System.Security.Claims;
using InfinitoCoffee.Application.Authentication.Dtos;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.Authentication;

public sealed class UserClaimsPrincipalFactory
{
    public ClaimsPrincipal Create(AuthenticatedUserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(AuthenticationConstants.DisplayNameClaimType, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            AuthenticationConstants.CookieScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    public bool TryRead(ClaimsPrincipal principal, out AuthenticatedUserDto? user)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = principal.FindFirstValue(ClaimTypes.Name);
        var displayName = principal.FindFirstValue(AuthenticationConstants.DisplayNameClaimType);
        var roleValue = principal.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(idValue, out var id)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(displayName)
            || !Enum.TryParse<UserRole>(roleValue, ignoreCase: false, out var role)
            || !Enum.IsDefined(role))
        {
            user = null;
            return false;
        }

        user = new AuthenticatedUserDto(id, username, displayName, role);
        return true;
    }
}
