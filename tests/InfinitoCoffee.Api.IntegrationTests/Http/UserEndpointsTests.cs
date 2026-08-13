using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InfinitoCoffee.Api.Contracts.Users;
using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class UserEndpointsTests
{
    public static IEnumerable<object[]> AuthorizationMatrix()
    {
        string[] endpoints = ["list", "get", "create", "update", "activate", "deactivate", "reset"];
        foreach (var endpoint in endpoints)
        {
            yield return [endpoint, null!, HttpStatusCode.Unauthorized];
            yield return [endpoint, (UserRole?)UserRole.Cashier, HttpStatusCode.Forbidden];
            yield return [endpoint, (UserRole?)UserRole.Kitchen, HttpStatusCode.Forbidden];
            yield return [endpoint, (UserRole?)UserRole.Administrator, SuccessStatus(endpoint)];
        }
    }

    [Theory]
    [MemberData(nameof(AuthorizationMatrix))]
    public async Task UsersEndpoints_EnforceAdministratorOnly(string endpoint, UserRole? role, HttpStatusCode expected)
    {
        await using var api = new ApiTestContext();
        var target = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "target.user", role: UserRole.Cashier));
        if (role.HasValue) await api.AuthenticateAsync(role.Value);

        using var request = CreateAuthorizationRequest(endpoint, target.Id);
        var response = role.HasValue && request.Method != HttpMethod.Get
            ? await api.SendWithCsrfAsync(request)
            : await api.Client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(response, expected);
    }

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Kitchen)]
    public async Task Create_ValidUser_PersistsNormalActiveUserAndReturnsSafeResponse(UserRole role)
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var response = await api.PostAsJsonWithCsrfAsync("/api/users", new
        {
            username = $"new.{role.ToString().ToLowerInvariant()}", displayName = " New User ", password = " secret preserved ", role
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalizedUsername", raw, StringComparison.OrdinalIgnoreCase);
        var result = JsonSerializer.Deserialize<UserResponse>(raw, JsonOptions)!;
        Assert.Equal(role, result.Role);
        Assert.True(result.IsActive);
        Assert.False(result.IsSystemUser);

        var persisted = await api.ExecuteDbContextAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == result.Id));
        Assert.NotEqual(" secret preserved ", persisted.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(persisted, persisted.PasswordHash, " secret preserved "));
    }

    [Fact]
    public async Task GetAllAndGetById_ReturnUsersWithoutSensitiveFields_AndUnknownReturns404()
    {
        await using var api = new ApiTestContext();
        var target = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "listed.user"));
        await api.AuthenticateAsync();
        var list = await api.Client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var raw = await list.Content.ReadAsStringAsync();
        Assert.Contains("listed.user", raw);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalizedUsername", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync($"/api/users/{target.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateWithDifferentCasing_Returns409()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "Juan"));
        await api.AuthenticateAsync();
        var response = await api.PostAsJsonWithCsrfAsync("/api/users", new { username = "juan", displayName = "Other", password = "secret", role = "Cashier" });
        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(response, HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("bad username", "valid")]
    [InlineData("valid.user", null)]
    [InlineData("valid.user", "")]
    [InlineData("valid.user", "   ")]
    public async Task Create_InvalidUsernameOrPassword_Returns400(string username, string? password)
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var response = await api.PostAsJsonWithCsrfAsync("/api/users", new { username, displayName = "User", password, role = "Cashier" });
        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_PasswordOver256_Returns400()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var response = await api.PostAsJsonWithCsrfAsync("/api/users", new { username = "valid.user", displayName = "User", password = new string('p', 257), role = "Cashier" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOtherUser_ChangesFieldsAndNormalization()
    {
        await using var api = new ApiTestContext();
        var target = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "old.user", role: UserRole.Cashier));
        await api.AuthenticateAsync();
        var response = await api.PutAsJsonWithCsrfAsync($"/api/users/{target.Id}", new { username = "New.User", displayName = "New Display", role = "Kitchen" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var persisted = await api.ExecuteDbContextAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == target.Id));
        Assert.Equal("NEW.USER", persisted.NormalizedUsername);
        Assert.Equal(UserRole.Kitchen, persisted.Role);
    }

    [Fact]
    public async Task SelfUsernameAndDisplayAreAllowed_ButRoleChangeAndDeactivateAreRejectedWithoutMutation()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var id = api.CurrentUserId!.Value;
        var update = await api.PutAsJsonWithCsrfAsync($"/api/users/{id}", new { username = "admin.renamed", displayName = "Renamed Admin", role = "Administrator" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var roleChange = await api.PutAsJsonWithCsrfAsync($"/api/users/{id}", new { username = "should.not.persist", displayName = "Should Not Persist", role = "Cashier" });
        Assert.Equal(HttpStatusCode.Conflict, roleChange.StatusCode);
        var deactivate = await api.PostWithCsrfAsync($"/api/users/{id}/deactivate");
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        var persisted = await api.ExecuteDbContextAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == id));
        Assert.Equal("admin.renamed", persisted.Username);
        Assert.Equal("Renamed Admin", persisted.DisplayName);
        Assert.Equal(UserRole.Administrator, persisted.Role);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task ActivateDeactivateOtherAndResetPasswordsIncludingSelf_WorkAsApproved()
    {
        await using var api = new ApiTestContext();
        var target = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "inactive.user", isActive: false));
        await api.AuthenticateAsync();
        Assert.Equal(HttpStatusCode.OK, (await api.PostWithCsrfAsync($"/api/users/{target.Id}/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PostWithCsrfAsync($"/api/users/{target.Id}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.PostAsJsonWithCsrfAsync($"/api/users/{target.Id}/reset-password", new { newPassword = "target-new" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.PostAsJsonWithCsrfAsync($"/api/users/{api.CurrentUserId}/reset-password", new { newPassword = "self-new" })).StatusCode);
        var users = await api.ExecuteDbContextAsync(db => db.Users.AsNoTracking().ToArrayAsync());
        var persistedTarget = users.Single(x => x.Id == target.Id);
        var persistedSelf = users.Single(x => x.Id == api.CurrentUserId);
        Assert.False(persistedTarget.IsActive);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(persistedTarget, persistedTarget.PasswordHash, "target-new"));
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(persistedSelf, persistedSelf.PasswordHash, "self-new"));
    }

    [Fact]
    public async Task SystemUser_IsListedButEveryMutationReturns409AndPreservesDatabase()
    {
        await using var api = new ApiTestContext();
        var system = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddSystemUserAsync(db));
        await api.AuthenticateAsync();
        var listJson = await api.Client.GetStringAsync("/api/users");
        Assert.Contains(JsonSerializer.Deserialize<UserResponse[]>(listJson, JsonOptions)!, x => x.Id == system.Id && x.IsSystemUser);
        var before = await ReadSnapshot(api, system.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PutAsJsonWithCsrfAsync($"/api/users/{system.Id}", new { username = "new.root", displayName = "New Root", role = "Administrator" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostWithCsrfAsync($"/api/users/{system.Id}/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostWithCsrfAsync($"/api/users/{system.Id}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync($"/api/users/{system.Id}/reset-password", new { newPassword = "new-secret" })).StatusCode);
        Assert.Equal(before, await ReadSnapshot(api, system.Id));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    [InlineData("reset")]
    public async Task MutableUsersEndpoints_RequireCsrf(string endpoint)
    {
        await using var api = new ApiTestContext();
        var target = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "csrf.target"));
        await api.AuthenticateAsync();
        using var request = CreateAuthorizationRequest(endpoint, target.Id);
        var response = await api.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEndpoint_DoesNotExist()
    {
        await using var api = new ApiTestContext();
        var target = await api.ExecuteDbContextAsync(db => TestDataSeeder.AddUserAsync(db, "delete.target"));
        await api.AuthenticateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{target.Id}");
        var response = await api.SendWithCsrfAsync(request);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthorizationRequest(string endpoint, Guid targetId) => endpoint switch
    {
        "list" => new(HttpMethod.Get, "/api/users"),
        "get" => new(HttpMethod.Get, $"/api/users/{targetId}"),
        "create" => Json(HttpMethod.Post, "/api/users", new { username = "matrix.created", displayName = "Matrix", password = "secret", role = "Cashier" }),
        "update" => Json(HttpMethod.Put, $"/api/users/{targetId}", new { username = "matrix.updated", displayName = "Matrix Updated", role = "Kitchen" }),
        "activate" => Json(HttpMethod.Post, $"/api/users/{targetId}/activate", new { }),
        "deactivate" => Json(HttpMethod.Post, $"/api/users/{targetId}/deactivate", new { }),
        "reset" => Json(HttpMethod.Post, $"/api/users/{targetId}/reset-password", new { newPassword = "new-secret" }),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
    };

    private static HttpRequestMessage Json<T>(HttpMethod method, string uri, T value) => new(method, uri) { Content = JsonContent.Create(value) };
    private static HttpStatusCode SuccessStatus(string endpoint) => endpoint switch { "create" => HttpStatusCode.Created, "reset" => HttpStatusCode.NoContent, _ => HttpStatusCode.OK };
    private static Task<string> ReadSnapshot(ApiTestContext api, Guid id) => api.ExecuteDbContextAsync(async db =>
    {
        var x = await db.Users.AsNoTracking().SingleAsync(user => user.Id == id);
        return $"{x.Username}|{x.DisplayName}|{x.PasswordHash}|{x.Role}|{x.IsActive}|{x.IsSystemUser}";
    });
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
}
