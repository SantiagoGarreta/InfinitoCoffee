using System.Net.Http.Json;
using InfinitoCoffee.Api.Antiforgery;
using InfinitoCoffee.Api.Contracts.Authentication;
using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Infrastructure.Persistence;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

internal sealed class ApiTestContext : IAsyncDisposable
{
    public ApiTestContext(string environmentName = "Development")
    {
        Factory = new TestApiApplicationFactory(environmentName);
        Client = Factory.CreateClient();
    }

    public TestApiApplicationFactory Factory { get; }

    public HttpClient Client { get; }

    public string? CurrentCsrfToken { get; private set; }

    public string? CurrentAuthenticationCookie { get; private set; }

    public Guid? CurrentUserId { get; private set; }

    public Task ExecuteDbContextAsync(Func<InfinitoCoffeeDbContext, Task> action)
    {
        return Factory.ExecuteDbContextAsync(action);
    }

    public Task<T> ExecuteDbContextAsync<T>(Func<InfinitoCoffeeDbContext, Task<T>> action)
    {
        return Factory.ExecuteDbContextAsync(action);
    }

    public async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(payload);
        return payload;
    }

    public async Task<string> GetCsrfTokenAsync()
    {
        var response = await Client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        CurrentCsrfToken = (await ReadRequiredAsync<CsrfTokenResponse>(response)).Token;
        return CurrentCsrfToken;
    }

    public async Task AuthenticateAsync(UserRole role = UserRole.Administrator)
    {
        const string password = "Correct_password!";
        var username = $"test.{role.ToString().ToLowerInvariant()}";
        var currentUser = await ExecuteDbContextAsync(dbContext =>
            TestDataSeeder.AddUserAsync(
                dbContext,
                username,
                role.ToString(),
                password,
                role));
        CurrentUserId = currentUser.Id;

        var token = await GetCsrfTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest
            {
                Username = username,
                Password = password
            })
        };
        request.Headers.Add(AntiforgeryConstants.HeaderName, token);
        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        CurrentAuthenticationCookie = response.Headers
            .GetValues("Set-Cookie")
            .Single()
            .Split(';', 2)[0];
        await GetCsrfTokenAsync();
    }

    public Task<HttpResponseMessage> PostWithCsrfAsync(string requestUri, HttpContent? content = null)
    {
        return SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        });
    }

    public Task<HttpResponseMessage> PostAsJsonWithCsrfAsync<T>(string requestUri, T value)
    {
        return SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(value)
        });
    }

    public Task<HttpResponseMessage> PostAsJsonWithCsrfAsync<T>(string requestUri, T value, string token)
    {
        return SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(value)
        }, token);
    }

    public Task<HttpResponseMessage> PutAsJsonWithCsrfAsync<T>(string requestUri, T value)
    {
        return SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(value)
        });
    }

    public Task<HttpResponseMessage> SendWithCsrfAsync(HttpRequestMessage request, string? token = null)
    {
        var requestToken = token ?? CurrentCsrfToken
            ?? throw new InvalidOperationException("A CSRF token must be obtained before sending a mutable request.");
        request.Headers.Add(AntiforgeryConstants.HeaderName, requestToken);
        return Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
