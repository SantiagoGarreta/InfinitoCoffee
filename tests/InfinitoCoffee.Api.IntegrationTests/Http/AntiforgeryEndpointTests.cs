using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.Antiforgery;
using InfinitoCoffee.Api.Contracts.Authentication;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class AntiforgeryEndpointTests
{
    private const string Password = "Correct_password!";

    [Fact]
    public async Task Csrf_WhenAnonymous_ReturnsOnlyRequestTokenAndSecureCookie()
    {
        await using var api = new ApiTestContext();
        var response = await api.Client.GetAsync("/api/auth/csrf");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var payload = await api.ReadRequiredAsync<CsrfTokenResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"token\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("username", body, StringComparison.OrdinalIgnoreCase);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{AntiforgeryConstants.CookieName}=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AntiforgeryOptions_UseApprovedDevelopmentAndProductionSettings()
    {
        await using var developmentApi = new ApiTestContext("Development");
        var developmentOptions = GetOptions(developmentApi);
        Assert.Equal(AntiforgeryConstants.CookieName, developmentOptions.Cookie.Name);
        Assert.Equal(AntiforgeryConstants.HeaderName, developmentOptions.HeaderName);
        Assert.True(developmentOptions.Cookie.HttpOnly);
        Assert.True(developmentOptions.Cookie.IsEssential);
        Assert.Equal("/", developmentOptions.Cookie.Path);
        Assert.Equal(SameSiteMode.Strict, developmentOptions.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, developmentOptions.Cookie.SecurePolicy);

        await using var productionApi = new ApiTestContext("Production");
        Assert.Equal(CookieSecurePolicy.Always, GetOptions(productionApi).Cookie.SecurePolicy);
    }

    [Fact]
    public async Task Login_WithoutRequestToken_Returns400WithoutSession()
    {
        await using var api = new ApiTestContext();
        await AddCashierAsync(api);
        await api.GetCsrfTokenAsync();

        var response = await api.Client.PostAsJsonAsync("/api/auth/login", Credentials());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoAuthenticationCookie(response);
    }

    [Fact]
    public async Task Login_WithInvalidRequestToken_Returns400WithoutSession()
    {
        await using var api = new ApiTestContext();
        await AddCashierAsync(api);
        await api.GetCsrfTokenAsync();

        var response = await api.PostAsJsonWithCsrfAsync(
            "/api/auth/login",
            Credentials(),
            "invalid-antiforgery-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoAuthenticationCookie(response);
    }

    [Fact]
    public async Task Login_WithValidPair_SucceedsAndAnonymousTokenCannotProtectAuthenticatedMutation()
    {
        await using var api = new ApiTestContext();
        await AddCashierAsync(api);
        var anonymousToken = await api.GetCsrfTokenAsync();

        var login = await api.PostAsJsonWithCsrfAsync("/api/auth/login", Credentials());
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var rejected = await api.SendWithCsrfAsync(
            new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(new { })
            },
            anonymousToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var authenticatedToken = await api.GetCsrfTokenAsync();
        Assert.NotEqual(anonymousToken, authenticatedToken);
    }

    [Fact]
    public async Task Logout_WhenAuthenticatedWithoutToken_Returns400AndSessionRemainsActive()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);

        var response = await api.Client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_WhenAuthenticatedWithToken_Returns204()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);

        var response = await api.PostWithCsrfAsync("/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedGet_DoesNotRequireAntiforgeryHeader()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);

        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/products")).StatusCode);
    }

    [Fact]
    public async Task CancelOrder_WithoutRequestToken_Returns400AndDoesNotMutateOrder()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(
                dbContext,
                "CSRF-CANCEL",
                OrderStatus.Pending,
                DateTime.UtcNow.AddMinutes(-2))).Id);

        var response = await api.Client.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var persistedStatus = await api.ExecuteDbContextAsync(async dbContext =>
            (await dbContext.Orders.FindAsync(orderId))!.Status);
        Assert.Equal(OrderStatus.Pending, persistedStatus);
    }

    private static async Task AddCashierAsync(ApiTestContext api)
    {
        await api.ExecuteDbContextAsync(dbContext => TestDataSeeder.AddUserAsync(
            dbContext,
            "cashier",
            "Cashier",
            Password,
            UserRole.Cashier));
    }

    private static LoginRequest Credentials()
    {
        return new LoginRequest { Username = "cashier", Password = Password };
    }

    private static void AssertNoAuthenticationCookie(HttpResponseMessage response)
    {
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var values)
            && values.Any(value => value.StartsWith("InfinitoCoffee.Auth=", StringComparison.Ordinal)));
    }

    private static AntiforgeryOptions GetOptions(ApiTestContext api)
    {
        return api.Factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
    }
}
