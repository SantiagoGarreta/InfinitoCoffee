using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.Contracts.ProductCategories;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class ProductCategoryEndpointsTests
{
    [Fact]
    public async Task CreateCategory_ReturnsCreated()
    {
        await using var api = new ApiTestContext();

        var response = await api.Client.PostAsJsonAsync(
            "/api/product-categories",
            new CreateProductCategoryRequest { Name = "Coffee" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await api.ReadRequiredAsync<ProductCategoryResponse>(response);
        Assert.Equal("Coffee", category.Name);
        Assert.True(category.IsActive);
    }

    [Fact]
    public async Task GetCategory_WhenExists_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext)).Id);

        var response = await api.Client.GetAsync($"/api/product-categories/{categoryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var category = await api.ReadRequiredAsync<ProductCategoryResponse>(response);
        Assert.Equal(categoryId, category.Id);
    }

    [Fact]
    public async Task GetCategory_WhenMissing_ReturnsProblemDetails404()
    {
        await using var api = new ApiTestContext();

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.Client.GetAsync($"/api/product-categories/{Guid.NewGuid()}"),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCategory_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext, "Coffee")).Id);

        var response = await api.Client.PutAsJsonAsync(
            $"/api/product-categories/{categoryId}",
            new UpdateProductCategoryRequest { Name = "Hot Coffee" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var category = await api.ReadRequiredAsync<ProductCategoryResponse>(response);
        Assert.Equal("Hot Coffee", category.Name);
    }

    [Fact]
    public async Task ActivateAndDeactivateCategory_ReturnOk()
    {
        await using var api = new ApiTestContext();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext, isActive: false)).Id);

        var activateResponse = await api.Client.PostAsync($"/api/product-categories/{categoryId}/activate", null);
        var deactivateResponse = await api.Client.PostAsync($"/api/product-categories/{categoryId}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
    }
}
