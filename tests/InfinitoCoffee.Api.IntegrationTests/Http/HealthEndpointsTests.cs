using System.Net;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task GetHealth_WithAvailableDatabase_ReturnsOk()
    {
        await using var api = new ApiTestContext();

        var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
