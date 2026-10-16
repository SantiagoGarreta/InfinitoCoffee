using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class PreparedToOrderTests
{
    [Fact]
    public async Task CoffeeConsumesRecipeGramsWhenReadyWithoutFinishedBalanceOrSecondDeduction()
    {
        await using var api = new ApiTestContext();
        var data = await SetupCoffee(api, "Cafes");
        await AddCoffeeStock(api, data.IngredientId, 100);

        var order = await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = data.ProductId, Quantity = 2 }]
        }));
        Assert.Equal(100m, await Quantity(api, data.IngredientId));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/start-preparation")).EnsureSuccessStatusCode();
        Assert.Equal(100m, await Quantity(api, data.IngredientId));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/mark-ready")).EnsureSuccessStatusCode();
        Assert.Equal(64m, await Quantity(api, data.IngredientId));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/deliver")).EnsureSuccessStatusCode();
        Assert.Equal(64m, await Quantity(api, data.IngredientId));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/void")).EnsureSuccessStatusCode();
        Assert.Equal(64m, await Quantity(api, data.IngredientId));
        var history = await Read<StockHistoryDto>(await api.Client.GetAsync(
            $"/api/stock/history?itemId={data.CoffeeItemId}"));
        Assert.Equal("Preparation", Assert.Single(history.Items).Type);
        await api.ExecuteDbContextAsync(async db =>
        {
            Assert.False(await db.StockBalances.AnyAsync(x => x.ItemId == data.CoffeeItemId));
            var preparation = Assert.Single(await db.StockOperations.Include(x => x.Movements)
                .Where(x => x.OrderId == order.Id).ToArrayAsync());
            Assert.Equal("Preparation", preparation.Type);
            Assert.Equal(-36m, Assert.Single(preparation.Movements).Delta);
            Assert.Equal(data.IngredientId, Assert.Single(preparation.Movements).ItemId);
        });
    }

    [Fact]
    public async Task InsufficientCoffeePreventsReadyAndLeavesIngredientStockUntouched()
    {
        await using var api = new ApiTestContext();
        var data = await SetupCoffee(api, "Cafes");
        await AddCoffeeStock(api, data.IngredientId, 20);
        var order = await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = data.ProductId, Quantity = 2 }]
        }));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/start-preparation")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/mark-ready")).StatusCode);
        Assert.Equal(20m, await Quantity(api, data.IngredientId));
        await api.ExecuteDbContextAsync(async db =>
        {
            Assert.Equal(OrderStatus.Preparing, (await db.Orders.SingleAsync(x => x.Id == order.Id)).Status);
            Assert.False(await db.StockOperations.AnyAsync(x => x.OrderId == order.Id));
        });
    }

    [Fact]
    public async Task CoffeeWithoutRecipeCannotBeSold()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db, "Cafes")).Id);
        var product = await api.ReadRequiredAsync<ProductResponse>(await api.PostAsJsonWithCsrfAsync("/api/products",
            new CreateProductRequest { Name = "Espresso", Price = 100, Cost = 30, CategoryId = categoryId,
                StockMode = ProductStockMode.MadeToOrder }));
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 }]
        })).StatusCode);
        Assert.Equal(0, await api.ExecuteDbContextAsync(db => db.Orders.CountAsync()));
    }

    [Fact]
    public async Task CantinaCoffeeConsumesIngredientsAtCheckout()
    {
        await using var api = new ApiTestContext();
        var data = await SetupCoffee(api, "Cantina");
        await AddCoffeeStock(api, data.IngredientId, 50);
        var order = await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = data.ProductId, Quantity = 1 }]
        }));
        Assert.Equal("Delivered", order.Status);
        Assert.Equal(32m, await Quantity(api, data.IngredientId));
        Assert.Equal(1, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(x => x.OrderId == order.Id)));
    }

    private static async Task<(Guid ProductId, Guid CoffeeItemId, Guid IngredientId)> SetupCoffee(
        ApiTestContext api, string categoryName)
    {
        await api.AuthenticateAsync();
        var categoryId = await api.ExecuteDbContextAsync(async db =>
            (await TestDataSeeder.AddCategoryAsync(db, categoryName)).Id);
        var product = await api.ReadRequiredAsync<ProductResponse>(await api.PostAsJsonWithCsrfAsync("/api/products",
            new CreateProductRequest { Name = "Capuchino", Price = 150, Cost = 50, CategoryId = categoryId,
                StockMode = ProductStockMode.MadeToOrder }));
        Assert.Equal(ProductStockMode.MadeToOrder, product.StockMode);
        Assert.Null(product.MinimumQuantity);
        var coffeeItem = await api.ExecuteDbContextAsync(db => db.StockItems.SingleAsync(x => x.ProductId == product.Id));
        Assert.Equal(StockItemKind.MadeToOrder, coffeeItem.Kind);
        Assert.False(await api.ExecuteDbContextAsync(db => db.StockBalances.AnyAsync(x => x.ItemId == coffeeItem.Id)));
        var ingredient = await Read<StockItemDto>(await api.PostAsJsonWithCsrfAsync("/api/stock/items",
            new StockItemRequest("Cafe molido", StockItemKind.Ingredient, StockUnit.Gram, null)));
        (await api.PostAsJsonWithCsrfAsync("/api/stock/recipes",
            new RecipeRequest(coffeeItem.Id, 1, [new StockLineInput(ingredient.Id, 18, "g")]))).EnsureSuccessStatusCode();
        return (product.Id, coffeeItem.Id, ingredient.Id);
    }

    private static async Task AddCoffeeStock(ApiTestContext api, Guid ingredientId, decimal grams) =>
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), "Compra de café", [new(ingredientId, grams, "g")]))).EnsureSuccessStatusCode();

    private static Task<decimal> Quantity(ApiTestContext api, Guid itemId) =>
        api.ExecuteDbContextAsync(async db => (await db.StockBalances.SingleAsync(x => x.ItemId == itemId)).Quantity);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } }))!;
    }
}
