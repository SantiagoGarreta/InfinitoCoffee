using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.Authentication;
using InfinitoCoffee.Api.Contracts.Authentication;
using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class AuthEndpointsTests
{
    private const string ValidPassword = "Correct_password!";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUserAndSecureSessionCookie()
    {
        await using var api = new ApiTestContext();
        var user = await api.ExecuteDbContextAsync(dbContext =>
            TestDataSeeder.AddUserAsync(
                dbContext,
                "Cashier.One",
                "Cashier One",
                ValidPassword,
                UserRole.Cashier));

        var response = await LoginAsync(api, "  CASHIER.ONE  ", ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authenticatedUser = await api.ReadRequiredAsync<AuthenticatedUserResponse>(response);
        Assert.Equal(user.Id, authenticatedUser.Id);
        Assert.Equal("Cashier.One", authenticatedUser.Username);
        Assert.Equal("Cashier One", authenticatedUser.DisplayName);
        Assert.Equal("Cashier", authenticatedUser.Role);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ValidPassword, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isSystemUser", responseBody, StringComparison.OrdinalIgnoreCase);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{AuthenticationConstants.CookieName}=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithSystemUser_ReturnsAdministratorWithoutExposingSystemFlag()
    {
        await using var api = new ApiTestContext();
        var user = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var passwordHasher = new PasswordHasher<User>();
            var systemUser = User.CreateSystemUser(
                "root.admin",
                "Root Administrator",
                candidate => passwordHasher.HashPassword(candidate, ValidPassword));
            dbContext.Users.Add(systemUser);
            await dbContext.SaveChangesAsync();
            return systemUser;
        });

        var response = await LoginAsync(api, "root.admin", ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authenticatedUser = await api.ReadRequiredAsync<AuthenticatedUserResponse>(response);
        Assert.Equal(user.Id, authenticatedUser.Id);
        Assert.Equal("Administrator", authenticatedUser.Role);
        Assert.DoesNotContain(
            "isSystemUser",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGenericProblemDetails()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(dbContext => TestDataSeeder.AddUserAsync(dbContext));

        var problem = await AssertInvalidCredentialsAsync(
            await LoginAsync(api, "admin", "wrong-password"));

        AssertGenericAuthenticationFailure(problem);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ReturnsSameGenericProblemDetails()
    {
        await using var api = new ApiTestContext();

        var problem = await AssertInvalidCredentialsAsync(
            await LoginAsync(api, "unknown", "wrong-password"));

        AssertGenericAuthenticationFailure(problem);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsSameGenericProblemDetails()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(dbContext =>
            TestDataSeeder.AddUserAsync(dbContext, isActive: false));

        var problem = await AssertInvalidCredentialsAsync(
            await LoginAsync(api, "admin", ValidPassword));

        AssertGenericAuthenticationFailure(problem);
    }

    [Fact]
    public async Task Login_WhenPasswordNeedsRehash_UpdatesHashBeforeCreatingSession()
    {
        await using var api = new ApiTestContext();
        var legacyHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            IterationCount = 1_000
        }));
        var user = await api.ExecuteDbContextAsync(dbContext =>
            TestDataSeeder.AddUserAsync(
                dbContext,
                password: ValidPassword,
                passwordHasher: legacyHasher));
        var originalHash = user.PasswordHash;

        var response = await LoginAsync(api, "admin", ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var persistedUser = await api.ExecuteDbContextAsync(dbContext =>
            dbContext.Users.AsNoTracking().SingleAsync(candidate => candidate.Id == user.Id));
        Assert.NotEqual(originalHash, persistedUser.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(
                persistedUser,
                persistedUser.PasswordHash,
                ValidPassword));
    }

    [Fact]
    public async Task Me_WhenAuthenticated_ReturnsClaimsWithoutConsultingDatabase()
    {
        await using var api = new ApiTestContext();
        var user = await api.ExecuteDbContextAsync(dbContext =>
            TestDataSeeder.AddUserAsync(
                dbContext,
                "Kitchen.One",
                "Kitchen One",
                ValidPassword,
                UserRole.Kitchen));
        (await LoginAsync(api, "kitchen.one", ValidPassword)).EnsureSuccessStatusCode();
        await api.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Users.Remove(await dbContext.Users.SingleAsync(candidate => candidate.Id == user.Id));
            await dbContext.SaveChangesAsync();
        });

        var response = await api.Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authenticatedUser = await api.ReadRequiredAsync<AuthenticatedUserResponse>(response);
        Assert.Equal(user.Id, authenticatedUser.Id);
        Assert.Equal("Kitchen.One", authenticatedUser.Username);
        Assert.Equal("Kitchen One", authenticatedUser.DisplayName);
        Assert.Equal("Kitchen", authenticatedUser.Role);
    }

    [Fact]
    public async Task Me_WithoutAuthentication_ReturnsProblemDetails401WithoutRedirect()
    {
        await using var api = new ApiTestContext();

        var response = await api.Client.GetAsync("/api/auth/me");

        var problem = await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Unauthorized);
        Assert.Equal("Authentication Required", problem.Title);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_DeletesCookieAndSubsequentMeReturns401()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(dbContext => TestDataSeeder.AddUserAsync(dbContext));
        (await LoginAsync(api, "admin", ValidPassword)).EnsureSuccessStatusCode();

        var logoutResponse = await api.Client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        var deletionCookie = Assert.Single(logoutResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{AuthenticationConstants.CookieName}=", deletionCookie, StringComparison.Ordinal);
        Assert.Contains("expires=", deletionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await api.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAuthentication_Returns401WithoutRedirect()
    {
        await using var api = new ApiTestContext();

        var response = await api.Client.PostAsync("/api/auth/logout", content: null);

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Unauthorized);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Forbidden_WhenAuthenticated_ReturnsProblemDetails403WithoutRedirect()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(dbContext => TestDataSeeder.AddUserAsync(dbContext));
        (await LoginAsync(api, "admin", ValidPassword)).EnsureSuccessStatusCode();

        var response = await api.Client.GetAsync("/_integration-tests/authorization/forbidden");

        var problem = await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Forbidden);
        Assert.Equal("Access Denied", problem.Title);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task CookieOptions_UseApprovedDevelopmentAndProductionSettings()
    {
        await using var developmentApi = new ApiTestContext("Development");
        var developmentOptions = GetCookieOptions(developmentApi);

        Assert.Equal(AuthenticationConstants.CookieName, developmentOptions.Cookie.Name);
        Assert.True(developmentOptions.Cookie.HttpOnly);
        Assert.True(developmentOptions.Cookie.IsEssential);
        Assert.Equal("/", developmentOptions.Cookie.Path);
        Assert.Equal(SameSiteMode.Strict, developmentOptions.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, developmentOptions.Cookie.SecurePolicy);
        Assert.Equal(TimeSpan.FromHours(8), developmentOptions.ExpireTimeSpan);
        Assert.False(developmentOptions.SlidingExpiration);
        Assert.Equal(typeof(ApiCookieAuthenticationEvents), developmentOptions.EventsType);

        await using var productionApi = new ApiTestContext("Production");
        var productionOptions = GetCookieOptions(productionApi);
        Assert.Equal(CookieSecurePolicy.Always, productionOptions.Cookie.SecurePolicy);
    }

    [Fact]
    public async Task CorsPreflight_FromAllowedOrigin_AllowsCredentials()
    {
        await using var api = new ApiTestContext();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost:4200",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task CorsPreflight_FromDisallowedOrigin_DoesNotReturnCorsHeaders()
    {
        await using var api = new ApiTestContext();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://malicious.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await api.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ExistingEndpoints_RemainPublic()
    {
        await using var api = new ApiTestContext();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext)).Id);

        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/orders/pickup")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/health")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await api.Client.PostAsJsonAsync("/api/products", new
            {
                name = "Public product",
                description = "Still public during 4A",
                price = 100m,
                categoryId
            })).StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        ApiTestContext api,
        string username,
        string password)
    {
        return api.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
    }

    private static async Task<ProblemDetails> AssertInvalidCredentialsAsync(
        HttpResponseMessage response)
    {
        Assert.False(response.Headers.Contains("Set-Cookie"));
        return await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Unauthorized);
    }

    private static void AssertGenericAuthenticationFailure(ProblemDetails problem)
    {
        Assert.Equal("Authentication Failed", problem.Title);
        Assert.Equal("Invalid username or password.", problem.Detail);
    }

    private static CookieAuthenticationOptions GetCookieOptions(ApiTestContext api)
    {
        return api.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(AuthenticationConstants.CookieScheme);
    }
}
