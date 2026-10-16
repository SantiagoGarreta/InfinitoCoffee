using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;

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
                Cost = 62m,
                CategoryId = categoryId
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await api.ReadRequiredAsync<ProductResponse>(response);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal(62m, product.Cost);
    }

    [Fact]
    public async Task CreateProduct_RegistersFinishedStockItemWithMinimumAndZeroBalances()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db)).Id);

        var response = await api.PostAsJsonWithCsrfAsync("/api/products", new CreateProductRequest
        {
            Name = "Scones",
            Price = 150m,
            Cost = 62m,
            CategoryId = categoryId,
            MinimumQuantity = 6
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await api.ReadRequiredAsync<ProductResponse>(response);
        Assert.Equal(6m, product.MinimumQuantity);
        var listedProducts = await api.ReadRequiredAsync<IReadOnlyCollection<ProductResponse>>(
            await api.Client.GetAsync("/api/products"));
        Assert.Equal(6m, Assert.Single(listedProducts).MinimumQuantity);
        Assert.Equal(6m, (await api.ReadRequiredAsync<ProductResponse>(
            await api.Client.GetAsync($"/api/products/{product.Id}"))).MinimumQuantity);
        var dashboardResponse = await api.Client.GetAsync("/api/stock");
        dashboardResponse.EnsureSuccessStatusCode();
        var dashboard = await ReadStock<StockDashboardDto>(dashboardResponse);
        var item = Assert.Single(dashboard.Items, x => x.ProductId == product.Id);
        Assert.Equal("Scones", item.Name);
        Assert.Equal(StockItemKind.FinishedProduct, item.Kind);
        Assert.Equal(StockUnit.Unit, item.Unit);
        Assert.Equal(6m, item.MinimumQuantity);
        var balances = await api.ExecuteDbContextAsync(db => db.StockBalances.AsNoTracking()
            .Where(x => x.ItemId == item.Id).OrderBy(x => x.BranchId).ToArrayAsync());
        Assert.Equal([1, 2], balances.Select(x => x.BranchId).ToArray());
        Assert.All(balances, x => Assert.Equal(0m, x.Quantity));

        var ingredientResponse = await api.PostAsJsonWithCsrfAsync("/api/stock/items",
            new StockItemRequest("Harina", StockItemKind.Ingredient, StockUnit.Gram, null));
        ingredientResponse.EnsureSuccessStatusCode();
        var ingredient = await ReadStock<StockItemDto>(ingredientResponse);
        var recipeResponse = await api.PostAsJsonWithCsrfAsync("/api/stock/recipes",
            new RecipeRequest(item.Id, 1, [new StockLineInput(ingredient.Id, 100, "g")]));
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await ReadStock<RecipeDto>(recipeResponse);
        Assert.Equal(item.Id, recipe.ItemId);
    }

    [Fact]
    public async Task ProductCanSwitchBetweenStockedAndMadeToOrderOnlyWithZeroBalances()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db)).Id);
        var product = await api.ReadRequiredAsync<ProductResponse>(await api.PostAsJsonWithCsrfAsync("/api/products",
            new CreateProductRequest { Name = "Capuchino", Price = 150, Cost = 50,
                CategoryId = categoryId, MinimumQuantity = 2 }));
        var itemId = await api.ExecuteDbContextAsync(async db =>
            (await db.StockItems.SingleAsync(x => x.ProductId == product.Id)).Id);
        await api.ExecuteDbContextAsync(async db =>
        {
            (await db.StockBalances.SingleAsync(x => x.ItemId == itemId && x.BranchId == 1)).Quantity = 1;
            await db.SaveChangesAsync();
        });
        var request = new UpdateProductRequest { Name = "Capuchino", Price = 150, Cost = 50,
            CategoryId = categoryId, StockMode = ProductStockMode.MadeToOrder };
        Assert.Equal(HttpStatusCode.Conflict,
            (await api.PutAsJsonWithCsrfAsync($"/api/products/{product.Id}", request)).StatusCode);
        await api.ExecuteDbContextAsync(async db =>
        {
            (await db.StockBalances.SingleAsync(x => x.ItemId == itemId && x.BranchId == 1)).Quantity = 0;
            await db.SaveChangesAsync();
        });
        var prepared = await api.ReadRequiredAsync<ProductResponse>(
            await api.PutAsJsonWithCsrfAsync($"/api/products/{product.Id}", request));
        Assert.Equal(ProductStockMode.MadeToOrder, prepared.StockMode);
        Assert.Null(prepared.MinimumQuantity);
        Assert.False(await api.ExecuteDbContextAsync(db => db.StockBalances.AnyAsync(x => x.ItemId == itemId)));
        var stocked = await api.ReadRequiredAsync<ProductResponse>(await api.PutAsJsonWithCsrfAsync(
            $"/api/products/{product.Id}", new UpdateProductRequest
            { Name = "Capuchino", Price = 150, Cost = 50, CategoryId = categoryId,
                StockMode = ProductStockMode.Stocked, MinimumQuantity = 3 }));
        Assert.Equal(ProductStockMode.Stocked, stocked.StockMode);
        Assert.Equal(3m, stocked.MinimumQuantity);
        Assert.Equal(2, await api.ExecuteDbContextAsync(db => db.StockBalances.CountAsync(x => x.ItemId == itemId)));
    }

    [Fact]
    public async Task CreateProduct_WithStockNameAlreadyInUse_DoesNotSaveProduct()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db)).Id);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/items",
            new StockItemRequest("Scones", StockItemKind.Ingredient, StockUnit.Unit, null))).EnsureSuccessStatusCode();

        var response = await api.PostAsJsonWithCsrfAsync("/api/products", new CreateProductRequest
        {
            Name = "Scones", Price = 150m, Cost = 62m, CategoryId = categoryId, MinimumQuantity = 3
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var products = await api.ReadRequiredAsync<IReadOnlyCollection<ProductResponse>>(
            await api.Client.GetAsync("/api/products"));
        Assert.DoesNotContain(products, x => x.Name == "Scones");
    }

    [Fact]
    public async Task UpdateProduct_SynchronizesLinkedStockItemNameAndMinimum()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db)).Id);
        var product = await api.ReadRequiredAsync<ProductResponse>(await api.PostAsJsonWithCsrfAsync(
            "/api/products", new CreateProductRequest
            {
                Name = "Scones", Price = 150m, Cost = 62m, CategoryId = categoryId, MinimumQuantity = 3
            }));

        var response = await api.PutAsJsonWithCsrfAsync($"/api/products/{product.Id}", new UpdateProductRequest
        {
            Name = "Scones integrales", Price = 160m, Cost = 70m,
            CategoryId = categoryId, MinimumQuantity = 8
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(8m, (await api.ReadRequiredAsync<ProductResponse>(response)).MinimumQuantity);
        var dashboard = await ReadStock<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        var item = Assert.Single(dashboard.Items, x => x.ProductId == product.Id);
        Assert.Equal("Scones integrales", item.Name);
        Assert.Equal(8m, item.MinimumQuantity);
        Assert.Equal(2, await api.ExecuteDbContextAsync(db =>
            db.StockBalances.CountAsync(x => x.ItemId == item.Id)));

        var updateWithoutMinimum = await api.PutAsJsonWithCsrfAsync($"/api/products/{product.Id}",
            new UpdateProductRequest
            {
                Name = "Scones integrales", Price = 170m, Cost = 75m, CategoryId = categoryId
            });
        Assert.Equal(8m, (await api.ReadRequiredAsync<ProductResponse>(updateWithoutMinimum)).MinimumQuantity);
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
                    Cost = 62m,
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
                    Cost = 62m,
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
    public async Task UpdateLegacyProduct_RegistersMissingStockItem()
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
                Cost = 74m,
                CategoryId = data.CategoryId
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await api.ReadRequiredAsync<ProductResponse>(response);
        Assert.Equal("Latte grande", product.Name);
        Assert.Equal(180m, product.Price);
        Assert.Equal(74m, product.Cost);
        Assert.Equal(data.CategoryId, product.CategoryId);
        Assert.Equal(0m, product.MinimumQuantity);
        var stockItem = await api.ExecuteDbContextAsync(dbContext =>
            dbContext.StockItems.SingleAsync(item => item.ProductId == data.ProductId));
        Assert.Equal("Latte grande", stockItem.Name);
        Assert.Equal(2, await api.ExecuteDbContextAsync(dbContext =>
            dbContext.StockBalances.CountAsync(balance => balance.ItemId == stockItem.Id)));
    }

    [Fact]
    public async Task UpdateProduct_KeepingInactiveCategory_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var data = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext, isActive: false);
            var product = await TestDataSeeder.AddProductAsync(dbContext, category.Id);
            return (ProductId: product.Id, CategoryId: category.Id);
        });

        var response = await api.PutAsJsonWithCsrfAsync(
            $"/api/products/{data.ProductId}",
            new UpdateProductRequest
            {
                Name = "Latte grande",
                Description = "Updated while category is inactive",
                Price = 180m,
                Cost = 74m,
                CategoryId = data.CategoryId
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await api.ReadRequiredAsync<ProductResponse>(response);
        Assert.Equal(data.CategoryId, product.CategoryId);
        Assert.Equal("Latte grande", product.Name);
    }

    [Fact]
    public async Task UpdateProduct_ChangingToInactiveCategory_ReturnsProblemDetails400()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var data = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            var inactiveCategory = await TestDataSeeder.AddCategoryAsync(dbContext, "Seasonal", isActive: false);
            var product = await TestDataSeeder.AddProductAsync(dbContext, category.Id);
            return (ProductId: product.Id, CategoryId: inactiveCategory.Id);
        });

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.PutAsJsonWithCsrfAsync(
                $"/api/products/{data.ProductId}",
                new UpdateProductRequest
                {
                    Name = "Latte",
                    Price = 180m,
                    Cost = 74m,
                    CategoryId = data.CategoryId
                }),
            HttpStatusCode.BadRequest);
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
    public async Task DeactivateProduct_WithHistoricalHardDeletePayload_KeepsProductInDatabase()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Administrator);

        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            return (await TestDataSeeder.AddProductAsync(dbContext, category.Id)).Id;
        });

        var response = await api.PostAsJsonWithCsrfAsync(
            $"/api/products/{productId}/deactivate",
            new { hardDelete = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var productAfterDeactivate = await api.ExecuteDbContextAsync(async dbContext =>
            await dbContext.Products.FindAsync(productId));

        Assert.NotNull(productAfterDeactivate);
        Assert.False(productAfterDeactivate.IsActive);
    }

    [Fact]
    public async Task DeleteProduct_ArchivesProductAndLinkedStockItem()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Administrator);
        var categoryId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddCategoryAsync(dbContext)).Id);
        var created = await api.PostAsJsonWithCsrfAsync("/api/products", new CreateProductRequest
        {
            Name = "Latte", Price = 180m, Cost = 74m, CategoryId = categoryId, MinimumQuantity = 2
        });
        created.EnsureSuccessStatusCode();
        var productId = (await api.ReadRequiredAsync<ProductResponse>(created)).Id;

        var response = await api.SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{productId}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var archived = await api.ExecuteDbContextAsync(async db => new
        {
            Product = await db.Products.SingleAsync(x => x.Id == productId),
            Item = await db.StockItems.SingleAsync(x => x.ProductId == productId)
        });
        Assert.True(archived.Product.IsDeleted);
        Assert.True(archived.Item.IsDeleted);
        Assert.Empty(await api.ReadRequiredAsync<ProductResponse[]>(await api.Client.GetAsync("/api/products")));
        var dashboard = await ReadStock<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.DoesNotContain(dashboard.Items, item => item.ProductId == productId);
    }

    [Fact]
    public async Task DeleteLinkedStockItem_AlsoArchivesCatalogProduct()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Administrator);
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db)).Id);
        var created = await api.PostAsJsonWithCsrfAsync("/api/products", new CreateProductRequest
        {
            Name = "Scones", Price = 150m, Cost = 62m, CategoryId = categoryId, MinimumQuantity = 1
        });
        var productId = (await api.ReadRequiredAsync<ProductResponse>(created)).Id;
        var stockItemId = await api.ExecuteDbContextAsync(async db =>
            (await db.StockItems.SingleAsync(x => x.ProductId == productId)).Id);

        var response = await api.SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/stock/items/{stockItemId}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await api.Client.GetAsync($"/api/products/{productId}")).StatusCode);
        var archived = await api.ExecuteDbContextAsync(async db =>
            (await db.Products.SingleAsync(x => x.Id == productId)).IsDeleted);
        Assert.True(archived);
    }

    private static async Task<T> ReadStock<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        });
        Assert.NotNull(payload);
        return payload;
    }
}
