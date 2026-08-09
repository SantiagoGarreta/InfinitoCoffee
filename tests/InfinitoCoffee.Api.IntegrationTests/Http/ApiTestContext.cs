using System.Net.Http.Json;
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

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
