using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.Contracts.ProductCategories;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class ProductCategoryEndpointsTests
{
    [Fact]
    public async Task CreateCategory_ReturnsCreated()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();

        var response = await api.PostAsJsonWithCsrfAsync(
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
        await api.AuthenticateAsync(UserRole.Cashier);
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
        await api.AuthenticateAsync(UserRole.Cashier);

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.Client.GetAsync($"/api/product-categories/{Guid.NewGuid()}"),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCategory_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext, "Coffee")).Id);

        var response = await api.PutAsJsonWithCsrfAsync(
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
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext, isActive: false)).Id);

        var activateResponse = await api.PostWithCsrfAsync($"/api/product-categories/{categoryId}/activate");
        var deactivateResponse = await api.PostWithCsrfAsync($"/api/product-categories/{categoryId}/deactivate");

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_LeavesProductsUnassignedUntilEachIsReassigned()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Administrator);
        var ids = await api.ExecuteDbContextAsync(async db =>
        {
            var oldCategory = await TestDataSeeder.AddCategoryAsync(db, "Antigua");
            var newCategory = await TestDataSeeder.AddCategoryAsync(db, "Nueva");
            var product = await TestDataSeeder.AddProductAsync(db, oldCategory.Id, "Latte");
            return (OldCategoryId: oldCategory.Id, NewCategoryId: newCategory.Id, ProductId: product.Id);
        });

        var deleted = await api.SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/product-categories/{ids.OldCategoryId}"));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/api/product-categories/{ids.OldCategoryId}")).StatusCode);
        var productAfterDelete = await api.ReadRequiredAsync<ProductResponse>(
            await api.Client.GetAsync($"/api/products/{ids.ProductId}"));
        Assert.Null(productAfterDelete.CategoryId);
        Assert.Null(await api.ExecuteDbContextAsync(async db =>
            (await db.Products.AsNoTracking().SingleAsync(x => x.Id == ids.ProductId)).CategoryId));

        var reassigned = await api.PutAsJsonWithCsrfAsync($"/api/products/{ids.ProductId}", new UpdateProductRequest
        {
            Name = "Latte", Price = 100m, Cost = 30m, CategoryId = ids.NewCategoryId
        });
        Assert.Equal(HttpStatusCode.OK, reassigned.StatusCode);
        Assert.Equal(ids.NewCategoryId, (await api.ReadRequiredAsync<ProductResponse>(reassigned)).CategoryId);
    }
}
