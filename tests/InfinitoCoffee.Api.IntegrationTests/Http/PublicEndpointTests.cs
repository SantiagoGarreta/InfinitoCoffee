using System.Net;
using System.Text.Json;
using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class PublicEndpointTests
{
    [Fact]
    public async Task Pickup_WhenAnonymous_ReturnsOnlyReducedPublicFields()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(dbContext => TestDataSeeder.AddOrderAsync(
            dbContext,
            "PUBLIC-1",
            OrderStatus.Preparing,
            DateTime.UtcNow.AddMinutes(-2)));

        var response = await api.Client.GetAsync("/api/orders/pickup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var order = Assert.Single(document.RootElement.EnumerateArray().ToArray());
        Assert.Equal(
            ["createdAtUtc", "id", "orderNumber", "status"],
            order.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
        Assert.False(order.TryGetProperty("notes", out _));
        Assert.False(order.TryGetProperty("total", out _));
        Assert.False(order.TryGetProperty("items", out _));
        Assert.False(order.TryGetProperty("source", out _));
        Assert.False(order.TryGetProperty("readyAtUtc", out _));
    }

    [Fact]
    public async Task Health_WhenAnonymous_Returns200WithoutAntiforgery()
    {
        await using var api = new ApiTestContext();

        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Swagger_WhenAnonymous_IsAvailableOnlyInDevelopment()
    {
        await using var developmentApi = new ApiTestContext("Development");
        Assert.Equal(
            HttpStatusCode.OK,
            (await developmentApi.Client.GetAsync("/swagger/v1/swagger.json")).StatusCode);

        await using var productionApi = new ApiTestContext("Production");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await productionApi.Client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }
}
