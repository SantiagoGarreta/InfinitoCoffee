using System.Security.Claims;
using InfinitoCoffee.Api.Authentication;
using InfinitoCoffee.Application.Authentication.Dtos;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.IntegrationTests.Authentication;

public sealed class UserClaimsPrincipalFactoryTests
{
    [Fact]
    public void Create_UsesExpectedClaimsAndRoleConfiguration()
    {
        var user = new AuthenticatedUserDto(
            Guid.NewGuid(),
            "cashier.one",
            "Cashier One",
            UserRole.Cashier);
        var factory = new UserClaimsPrincipalFactory();

        var principal = factory.Create(user);

        Assert.Equal(user.Id.ToString("D"), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.Username, principal.Identity!.Name);
        Assert.Equal(user.DisplayName, principal.FindFirstValue(AuthenticationConstants.DisplayNameClaimType));
        Assert.Equal("Cashier", principal.FindFirstValue(ClaimTypes.Role));
        Assert.True(principal.IsInRole("Cashier"));
        Assert.Null(principal.FindFirst("IsSystemUser"));
    }

    [Fact]
    public void TryRead_WithExpectedClaims_ReturnsUser()
    {
        var expected = new AuthenticatedUserDto(
            Guid.NewGuid(),
            "kitchen.one",
            "Kitchen One",
            UserRole.Kitchen);
        var factory = new UserClaimsPrincipalFactory();

        var result = factory.TryRead(factory.Create(expected), out var actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("missing-id")]
    [InlineData("invalid-id")]
    [InlineData("missing-username")]
    [InlineData("missing-display-name")]
    [InlineData("invalid-role")]
    public void TryRead_WithMissingOrInvalidClaim_ReturnsFalse(string invalidClaim)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new(ClaimTypes.Name, "admin"),
            new(AuthenticationConstants.DisplayNameClaimType, "Administrator"),
            new(ClaimTypes.Role, "Administrator")
        };

        switch (invalidClaim)
        {
            case "missing-id":
                claims.RemoveAll(claim => claim.Type == ClaimTypes.NameIdentifier);
                break;
            case "invalid-id":
                claims.RemoveAll(claim => claim.Type == ClaimTypes.NameIdentifier);
                claims.Add(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
                break;
            case "missing-username":
                claims.RemoveAll(claim => claim.Type == ClaimTypes.Name);
                break;
            case "missing-display-name":
                claims.RemoveAll(claim => claim.Type == AuthenticationConstants.DisplayNameClaimType);
                break;
            case "invalid-role":
                claims.RemoveAll(claim => claim.Type == ClaimTypes.Role);
                claims.Add(new Claim(ClaimTypes.Role, "Owner"));
                break;
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            AuthenticationConstants.CookieScheme,
            ClaimTypes.Name,
            ClaimTypes.Role));

        var result = new UserClaimsPrincipalFactory().TryRead(principal, out var user);

        Assert.False(result);
        Assert.Null(user);
    }
}
