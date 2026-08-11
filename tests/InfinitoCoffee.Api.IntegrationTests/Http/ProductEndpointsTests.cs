using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class ProductEndpointsTests
{
    [Fact]
    public async Task CreateProduct_WithValidCategory_ReturnsCreated()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext)).Id);

        var response = await api.PostAsJsonWithCsrfAsync(
            "/api/products",
            new CreateProductRequest
            {
                Name = "Cafe latte",
                Description = "Cafe con leche",
                Price = 150m,
                CategoryId = categoryId
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await api.ReadRequiredAsync<ProductResponse>(response);
        Assert.Equal(categoryId, product.CategoryId);
    }

    [Fact]
    public async Task CreateProduct_WithMissingCategory_ReturnsProblemDetails404()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.PostAsJsonWithCsrfAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Name = "Cafe latte",
                    Description = "Cafe con leche",
                    Price = 150m,
                    CategoryId = Guid.NewGuid()
                }),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProduct_WithInactiveCategory_ReturnsProblemDetails400()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext, isActive: false)).Id);

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.PostAsJsonWithCsrfAsync(
                "/api/products",
                new CreateProductRequest
                {
                    Name = "Cafe latte",
                    Description = "Cafe con leche",
                    Price = 150m,
                    CategoryId = categoryId
                }),
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);
        await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            await TestDataSeeder.AddProductAsync(dbContext, category.Id, "Latte");
            await TestDataSeeder.AddProductAsync(dbContext, category.Id, "Espresso");
        });

        var response = await api.Client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await api.ReadRequiredAsync<IReadOnlyCollection<ProductResponse>>(response);
        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task UpdateProduct_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var data = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            var nextCategory = await TestDataSeeder.AddCategoryAsync(dbContext, "Special");
            var product = await TestDataSeeder.AddProductAsync(dbContext, category.Id);
            return (ProductId: product.Id, CategoryId: nextCategory.Id);
        });

        var response = await api.PutAsJsonWithCsrfAsync(
            $"/api/products/{data.ProductId}",
            new UpdateProductRequest
            {
                Name = "Latte grande",
                Description = "Cafe con leche grande",
                Price = 180m,
                CategoryId = data.CategoryId
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await api.ReadRequiredAsync<ProductResponse>(response);
        Assert.Equal("Latte grande", product.Name);
        Assert.Equal(180m, product.Price);
        Assert.Equal(data.CategoryId, product.CategoryId);
    }

    [Fact]
    public async Task ActivateAndDeactivateProduct_ReturnOk()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            return (await TestDataSeeder.AddProductAsync(dbContext, category.Id, isActive: false)).Id;
        });

        var activateResponse = await api.PostWithCsrfAsync($"/api/products/{productId}/activate");
        var deactivateResponse = await api.PostWithCsrfAsync($"/api/products/{productId}/deactivate");

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_WithHardDeleteFlag_RemovesProductFromDatabase()
    {
        await using var api = new ApiTestContext();
        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            return (await TestDataSeeder.AddProductAsync(dbContext, category.Id)).Id;
        });

        var response = await api.Client.PostAsJsonAsync(
            $"/api/products/{productId}/deactivate",
            new { hardDelete = true });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var existsAfterDelete = await api.ExecuteDbContextAsync(async dbContext =>
            await dbContext.Products.FindAsync(productId) is not null);

        Assert.False(existsAfterDelete);
    }
}
