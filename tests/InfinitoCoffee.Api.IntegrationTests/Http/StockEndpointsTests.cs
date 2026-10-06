using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class StockEndpointsTests
{
    [Fact]
    public async Task CantinaSaleConsumesStockWhenOrderIsCreated()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var productId = await api.ExecuteDbContextAsync(async db =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(db, "Cantina");
            return (await TestDataSeeder.AddProductAsync(db, category.Id, "Chicles")).Id;
        });
        var stockItem = await CreateItem(api, new("Chicles", StockItemKind.FinishedProduct, StockUnit.Unit, productId));
        await Adjust(api, stockItem.Id, 5);

        var response = await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 2 }]
        });

        var order = await Read<OrderResponse>(response);
        Assert.Equal("Delivered", order.Status);
        Assert.Equal(3, await Quantity(api, stockItem.Id));
        Assert.Equal(1, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(
            operation => operation.Type == "Sale" && operation.OrderId == order.Id)));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/deliver")).StatusCode);
        Assert.Equal(3, await Quantity(api, stockItem.Id));
    }

    [Fact]
    public async Task MixedOrderConsumesEachProductAtItsOwnSaleStage()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var productIds = await api.ExecuteDbContextAsync(async db =>
        {
            var cantina = await TestDataSeeder.AddCategoryAsync(db, "Cantina");
            var cafes = await TestDataSeeder.AddCategoryAsync(db, "Cafes");
            var gum = await TestDataSeeder.AddProductAsync(db, cantina.Id, "Chicles");
            var coffee = await TestDataSeeder.AddProductAsync(db, cafes.Id, "Espresso");
            return (Gum: gum.Id, Coffee: coffee.Id);
        });
        var gumStock = await CreateItem(api, new("Chicles", StockItemKind.FinishedProduct, StockUnit.Unit, productIds.Gum));
        var coffeeStock = await CreateItem(api, new("Espresso", StockItemKind.FinishedProduct, StockUnit.Unit, productIds.Coffee));
        await Adjust(api, gumStock.Id, 5);
        await Adjust(api, coffeeStock.Id, 5);

        var order = await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest { ProductId = productIds.Gum, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = productIds.Coffee, Quantity = 1 }
            ]
        }));

        Assert.Equal("Pending", order.Status);
        Assert.Equal(3, await Quantity(api, gumStock.Id));
        Assert.Equal(5, await Quantity(api, coffeeStock.Id));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/start-preparation")).EnsureSuccessStatusCode();
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/mark-ready")).EnsureSuccessStatusCode();
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/deliver")).EnsureSuccessStatusCode();
        Assert.Equal(3, await Quantity(api, gumStock.Id));
        Assert.Equal(4, await Quantity(api, coffeeStock.Id));
        Assert.Equal(2, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(
            operation => operation.Type == "Sale" && operation.OrderId == order.Id)));
    }

    [Fact]
    public async Task ReceivingFinishedProductsConsumesRecipeIngredientsAndDeliveryOnlyConsumesProducts()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Adjust(api, setup.Egg.Id, 30);
        await Adjust(api, setup.Scone.Id, 10);
        Assert.Equal(25, await Quantity(api, setup.Egg.Id));
        Assert.Equal(10, await Quantity(api, setup.Scone.Id));
        var orderId = await ReadyOrder(api, setup.Scone.ProductId!.Value, 3);
        (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).EnsureSuccessStatusCode();
        Assert.Equal(7, await Quantity(api, setup.Scone.Id));
        Assert.Equal(25, await Quantity(api, setup.Egg.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).StatusCode);
        Assert.Equal(25, await Quantity(api, setup.Egg.Id));
        var history = await Read<StockHistoryDto>(await api.Client.GetAsync("/api/stock/history"));
        var sale = Assert.Single(history.Items, x => x.Type == "Sale");
        Assert.Equal(orderId, sale.OrderId);
        Assert.DoesNotContain(sale.Movements, x => x.ItemId == setup.Egg.Id);
        Assert.Contains(sale.Movements, x => x.ItemId == setup.Scone.Id && x.Delta == -3);
        var received = Assert.Single(history.Items, x => x.Type == "Adjustment" && x.RecipeId.HasValue);
        Assert.Contains(received.Movements, x => x.ItemId == setup.Egg.Id && x.Delta == -5);
        Assert.Contains(received.Movements, x => x.ItemId == setup.Scone.Id && x.Delta == 10);
    }

    [Fact]
    public async Task InsufficientIngredientRollsBackFinishedProductReceipt()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Adjust(api, setup.Egg.Id, 4);
        var request = new StockAdjustmentRequest(Guid.NewGuid(), "Llegaron scones", [new(setup.Scone.Id, 10, "unit")]);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", request)).StatusCode);
        Assert.Equal(0, await Quantity(api, setup.Scone.Id));
        Assert.Equal(4, await Quantity(api, setup.Egg.Id));
        Assert.Equal(1, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(x => x.Type == "Adjustment")));
    }

    [Fact]
    public async Task DeliveryWithoutEnoughFinishedProductDoesNotTouchIngredients()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Adjust(api, setup.Egg.Id, 30);
        await Adjust(api, setup.Scone.Id, 2);
        var orderId = await ReadyOrder(api, setup.Scone.ProductId!.Value, 3);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).StatusCode);
        Assert.Equal(2, await Quantity(api, setup.Scone.Id));
        Assert.Equal(29, await Quantity(api, setup.Egg.Id));
        Assert.Equal(OrderStatus.Ready, await api.ExecuteDbContextAsync(async db => (await db.Orders.SingleAsync(x => x.Id == orderId)).Status));
    }

    [Fact]
    public async Task ManualAdjustmentCanIncreaseOrDecreaseAndNeedsReason()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Adjust(api, setup.Egg.Id, 10);
        await Adjust(api, setup.Scone.Id, 10);
        await Adjust(api, setup.Scone.Id, -2, "Dos scones descartados");
        Assert.Equal(8, await Quantity(api, setup.Scone.Id));
        Assert.Equal(5, await Quantity(api, setup.Egg.Id));
        var invalid = new StockAdjustmentRequest(Guid.NewGuid(), "", [new(setup.Scone.Id, -1, "unit")]);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments",
            invalid with { OperationId = Guid.NewGuid(), Notes = "Pérdida", Lines = [new(setup.Scone.Id, -9, "unit")] })).StatusCode);
        Assert.Equal(8, await Quantity(api, setup.Scone.Id));
    }

    [Fact]
    public async Task AdjustmentRetryDoesNotDuplicateMovement()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Adjust(api, setup.Egg.Id, 30);
        var request = new StockAdjustmentRequest(Guid.NewGuid(), "Llegaron scones", [new(setup.Scone.Id, 10, "unit")]);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", request)).EnsureSuccessStatusCode();
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", request)).EnsureSuccessStatusCode();
        Assert.Equal(10, await Quantity(api, setup.Scone.Id));
        Assert.Equal(25, await Quantity(api, setup.Egg.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", request with { Notes = "Otro" })).StatusCode);
    }

    [Fact]
    public async Task NewRecipeVersionAppliesToFutureReceiptsButNotToSales()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Adjust(api, setup.Egg.Id, 30);
        await Adjust(api, setup.Scone.Id, 10);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/recipes",
            new RecipeRequest(setup.Scone.Id, 10, [new(setup.Egg.Id, 6, "unit")]))).EnsureSuccessStatusCode();
        var orderId = await ReadyOrder(api, setup.Scone.ProductId!.Value, 3);
        (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).EnsureSuccessStatusCode();
        Assert.Equal(25, await Quantity(api, setup.Egg.Id));
        await Adjust(api, setup.Scone.Id, 10);
        Assert.Equal(19, await Quantity(api, setup.Egg.Id));
        Assert.Equal(17, await Quantity(api, setup.Scone.Id));
    }

    [Fact]
    public async Task LegacyStationEndpointsAreGoneAndStockRemainsAdministratorOnly()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        foreach (var path in new[] { "production", "transfers", "waste", "counts" })
            Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsJsonWithCsrfAsync($"/api/stock/{path}", new { })).StatusCode);
        await api.AuthenticateAsync(UserRole.Cashier);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", new { })).StatusCode);
    }

    private static async Task<(StockItemDto Egg, StockItemDto Scone)> Setup(ApiTestContext api)
    {
        await api.AuthenticateAsync();
        var productId = await api.ExecuteDbContextAsync(async db =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(db, "Panadería");
            return (await TestDataSeeder.AddProductAsync(db, category.Id, "Scone")).Id;
        });
        var egg = await CreateItem(api, new("Huevos", StockItemKind.Ingredient, StockUnit.Unit, null));
        var scone = await CreateItem(api, new("Scones", StockItemKind.FinishedProduct, StockUnit.Unit, productId));
        (await api.PostAsJsonWithCsrfAsync("/api/stock/recipes", new RecipeRequest(scone.Id, 10, [new(egg.Id, 5, "unit")]))).EnsureSuccessStatusCode();
        return (egg, scone);
    }
    private static async Task<StockItemDto> CreateItem(ApiTestContext api, StockItemRequest request) =>
        await Read<StockItemDto>(await api.PostAsJsonWithCsrfAsync("/api/stock/items", request));
    private static async Task Adjust(ApiTestContext api, Guid id, decimal delta, string notes = "Stock inicial") =>
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", new StockAdjustmentRequest(Guid.NewGuid(), notes, [new(id, delta, "unit")]))).EnsureSuccessStatusCode();
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } }))!;
    }
    private static Task<decimal> Quantity(ApiTestContext api, Guid id) =>
        api.ExecuteDbContextAsync(async db => (await db.StockBalances.SingleAsync(x => x.ItemId == id && (x.BranchId == 0 || x.BranchId == 1))).Quantity);
    private static Task<Guid> ReadyOrder(ApiTestContext api, Guid productId, int quantity) => api.ExecuteDbContextAsync(async db =>
    {
        var order = new Order("1", DateTime.UtcNow, [new OrderItem(productId, "Scone", 50m, 10m, quantity)]);
        order.StartPreparing(DateTime.UtcNow);
        order.MarkReady(DateTime.UtcNow);
        db.Orders.Add(order); await db.SaveChangesAsync(); return order.Id;
    });
}
