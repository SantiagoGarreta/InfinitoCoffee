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
    public async Task VoidSale_ReversesEverySaleMovementAgainstCurrentBalanceAndPreservesAudit()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var productIds = await api.ExecuteDbContextAsync(async db =>
        {
            var cantina = await TestDataSeeder.AddCategoryAsync(db, "Cantina");
            var kitchen = await TestDataSeeder.AddCategoryAsync(db, "Cocina");
            return (
                Gum: (await TestDataSeeder.AddProductAsync(db, cantina.Id, "Chicles", 50m, 20m)).Id,
                Coffee: (await TestDataSeeder.AddProductAsync(db, kitchen.Id, "Espresso", 100m, 30m)).Id);
        });
        var gum = await CreateItem(api, new("Chicles", StockItemKind.FinishedProduct, StockUnit.Unit, productIds.Gum));
        var coffee = await CreateItem(api, new("Espresso", StockItemKind.FinishedProduct, StockUnit.Unit, productIds.Coffee));
        await Adjust(api, gum.Id, 5);
        await Adjust(api, coffee.Id, 5);
        var order = await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest { ProductId = productIds.Gum, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = productIds.Coffee, Quantity = 1 }
            ]
        }));
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/start-preparation")).EnsureSuccessStatusCode();
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/mark-ready")).EnsureSuccessStatusCode();
        (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/deliver")).EnsureSuccessStatusCode();
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), "Ingreso posterior", [new(coffee.Id, 5, "unit")], false))).EnsureSuccessStatusCode();

        var voided = await Read<OrderResponse>(await api.PostWithCsrfAsync($"/api/orders/{order.Id}/void"));

        Assert.Equal("Voided", voided.Status);
        Assert.Equal(5, await Quantity(api, gum.Id));
        Assert.Equal(10, await Quantity(api, coffee.Id));
        await api.ExecuteDbContextAsync(async db =>
        {
            var sales = await db.StockOperations.Include(x => x.Movements)
                .Where(x => x.OrderId == order.Id && x.Type == "Sale").ToArrayAsync();
            var reversals = await db.StockOperations.Include(x => x.Movements)
                .Where(x => x.OrderId == order.Id && x.Type == "SaleReversal").ToArrayAsync();
            Assert.Equal(2, sales.Length);
            Assert.Equal(2, reversals.Length);
            Assert.All(reversals, reversal => Assert.Equal(api.CurrentUserId, reversal.ActorId));
            Assert.Equal(sales.Select(x => x.Id).Order(), reversals.Select(x => x.ReversesOperationId!.Value).Order());
            Assert.Equal([1m, 2m], reversals.SelectMany(x => x.Movements).Select(x => x.Delta).Order().ToArray());
        });
        var results = await Read<OrderResultsResponse>(await api.Client.GetAsync("/api/orders/summary"));
        Assert.Equal(0m, results.CurrentPeriod.TotalRevenue);
        Assert.Empty(results.TopSellingProducts);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/void")).StatusCode);
        Assert.Equal(5, await Quantity(api, gum.Id));
        Assert.Equal(10, await Quantity(api, coffee.Id));
    }

    [Fact]
    public async Task VoidSale_WhenReversalFails_RollsBackOrderAndAudit()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var data = await api.ExecuteDbContextAsync(async db =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(db, "Cocina");
            var product = await TestDataSeeder.AddProductAsync(db, category.Id, "Torta");
            var item = new StockItem
            {
                Name = "Torta",
                NormalizedName = "TORTA",
                Kind = StockItemKind.FinishedProduct,
                Unit = StockUnit.Unit,
                ProductId = product.Id
            };
            db.StockItems.Add(item);
            db.StockBalances.Add(new StockBalance
            {
                ItemId = item.Id,
                BranchId = 1,
                Location = StockLocation.Factory,
                Quantity = StockQuantity.Maximum
            });
            var order = new Order("ROLLBACK", DateTime.UtcNow,
                [new OrderItem(product.Id, product.Name, product.Price, product.Cost, 1)]);
            order.StartPreparing(DateTime.UtcNow);
            order.MarkReady(DateTime.UtcNow);
            order.Deliver(DateTime.UtcNow);
            db.Orders.Add(order);
            var sale = new StockOperation
            {
                Id = Guid.NewGuid(),
                BranchId = 1,
                Type = "Sale",
                CreatedAtUtc = DateTime.UtcNow,
                OrderId = order.Id,
                Notes = "Venta",
                Movements = [new StockMovement
                {
                    ItemId = item.Id, BranchId = 1, Location = StockLocation.Factory,
                    Before = 2, Delta = -1, After = 1
                }]
            };
            db.StockOperations.Add(sale);
            await db.SaveChangesAsync();
            return (OrderId: order.Id, ItemId: item.Id);
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostWithCsrfAsync($"/api/orders/{data.OrderId}/void")).StatusCode);
        await api.ExecuteDbContextAsync(async db =>
        {
            Assert.Equal(OrderStatus.Delivered, (await db.Orders.SingleAsync(x => x.Id == data.OrderId)).Status);
            Assert.Equal(StockQuantity.Maximum, (await db.StockBalances.SingleAsync(x => x.ItemId == data.ItemId)).Quantity);
            Assert.False(await db.StockOperations.AnyAsync(x => x.OrderId == data.OrderId && x.Type == "SaleReversal"));
        });
    }

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

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    public async Task CancelMixedOrder_RestoresCantinaStockWithoutChangingUnsoldKitchenStock(OrderStatus status)
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var productIds = await api.ExecuteDbContextAsync(async db =>
        {
            var cantina = await TestDataSeeder.AddCategoryAsync(db, "Cantina");
            var kitchen = await TestDataSeeder.AddCategoryAsync(db, "Cocina");
            return (
                Cola: (await TestDataSeeder.AddProductAsync(db, cantina.Id, "Coca Cola", 80m, 30m)).Id,
                Sandwich: (await TestDataSeeder.AddProductAsync(db, kitchen.Id, "Sanguche", 150m, 60m)).Id);
        });
        var cola = await CreateItem(api, new("Coca Cola", StockItemKind.FinishedProduct, StockUnit.Unit, productIds.Cola));
        var sandwich = await CreateItem(api, new("Sanguche", StockItemKind.FinishedProduct, StockUnit.Unit, productIds.Sandwich));
        await Adjust(api, cola.Id, 5);
        await Adjust(api, sandwich.Id, 5);

        var order = await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest { ProductId = productIds.Cola, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = productIds.Sandwich, Quantity = 1 }
            ]
        }));
        Assert.Equal(3, await Quantity(api, cola.Id));
        Assert.Equal(5, await Quantity(api, sandwich.Id));

        if (status is OrderStatus.Preparing or OrderStatus.Ready)
            (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/start-preparation")).EnsureSuccessStatusCode();
        if (status == OrderStatus.Ready)
            (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/mark-ready")).EnsureSuccessStatusCode();

        var cancelled = await Read<OrderResponse>(await api.PostWithCsrfAsync($"/api/orders/{order.Id}/cancel"));

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(5, await Quantity(api, cola.Id));
        Assert.Equal(5, await Quantity(api, sandwich.Id));
        await api.ExecuteDbContextAsync(async db =>
        {
            var sale = Assert.Single(await db.StockOperations.Include(x => x.Movements)
                .Where(x => x.OrderId == order.Id && x.Type == "Sale").ToArrayAsync());
            var reversal = Assert.Single(await db.StockOperations.Include(x => x.Movements)
                .Where(x => x.OrderId == order.Id && x.Type == "SaleReversal").ToArrayAsync());
            Assert.Equal(sale.Id, reversal.ReversesOperationId);
            Assert.Equal(2m, Assert.Single(reversal.Movements).Delta);
            Assert.Equal(cola.Id, Assert.Single(reversal.Movements).ItemId);
        });
        var results = await Read<OrderResultsResponse>(await api.Client.GetAsync("/api/orders/summary"));
        Assert.Equal(0m, results.CurrentPeriod.TotalRevenue);
        Assert.Empty(results.TopSellingProducts);

        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostWithCsrfAsync($"/api/orders/{order.Id}/cancel")).StatusCode);
        Assert.Equal(5, await Quantity(api, cola.Id));
        Assert.Equal(1, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(
            x => x.OrderId == order.Id && x.Type == "SaleReversal")));
    }

    [Fact]
    public async Task CancelMixedOrder_WhenStockReversalFails_RollsBackCancellation()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var orderId = await api.ExecuteDbContextAsync(async db =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(db, "Cantina");
            var product = await TestDataSeeder.AddProductAsync(db, category.Id, "Coca Cola");
            var item = new StockItem
            {
                Name = "Coca Cola",
                NormalizedName = "COCA COLA",
                Kind = StockItemKind.FinishedProduct,
                Unit = StockUnit.Unit,
                ProductId = product.Id
            };
            db.StockItems.Add(item);
            db.StockBalances.Add(new StockBalance
            {
                ItemId = item.Id,
                BranchId = 1,
                Location = StockLocation.Factory,
                Quantity = StockQuantity.Maximum
            });
            var orderItem = new OrderItem(product.Id, product.Name, product.Price, product.Cost, 1);
            orderItem.MarkSoldAtCashRegister(DateTime.UtcNow);
            var order = new Order("CANCEL-ROLLBACK", DateTime.UtcNow, [orderItem]);
            order.StartPreparing(DateTime.UtcNow);
            db.Orders.Add(order);
            db.StockOperations.Add(new StockOperation
            {
                Id = Guid.NewGuid(),
                BranchId = 1,
                Type = "Sale",
                CreatedAtUtc = DateTime.UtcNow,
                OrderId = order.Id,
                Notes = "Venta",
                Movements = [new StockMovement
                {
                    ItemId = item.Id, BranchId = 1, Location = StockLocation.Factory,
                    Before = 2, Delta = -1, After = 1
                }]
            });
            await db.SaveChangesAsync();
            return order.Id;
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostWithCsrfAsync($"/api/orders/{orderId}/cancel")).StatusCode);
        await api.ExecuteDbContextAsync(async db =>
        {
            Assert.Equal(OrderStatus.Preparing, (await db.Orders.SingleAsync(x => x.Id == orderId)).Status);
            Assert.Equal(StockQuantity.Maximum, (await db.StockBalances.SingleAsync()).Quantity);
            Assert.False(await db.StockOperations.AnyAsync(x => x.OrderId == orderId && x.Type == "SaleReversal"));
        });
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

        (await api.PostWithCsrfAsync($"/api/orders/{orderId}/void")).EnsureSuccessStatusCode();
        Assert.Equal(10, await Quantity(api, setup.Scone.Id));
        Assert.Equal(25, await Quantity(api, setup.Egg.Id));
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

    [Fact]
    public async Task DeleteIngredient_RequiresZeroBalanceAndPreservesMovementHistory()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var item = await CreateItem(api, new("Harina", StockItemKind.Ingredient, StockUnit.Unit, null));
        await Adjust(api, item.Id, 3);

        var route = $"/api/stock/items/{item.Id}";
        Assert.Equal(HttpStatusCode.Conflict,
            (await api.SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Delete, route))).StatusCode);
        await Adjust(api, item.Id, -3);
        Assert.Equal(HttpStatusCode.NoContent,
            (await api.SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Delete, route))).StatusCode);

        var dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.DoesNotContain(dashboard.Items, x => x.Id == item.Id);
        var history = await Read<StockHistoryDto>(await api.Client.GetAsync($"/api/stock/history?itemId={item.Id}"));
        Assert.Equal(2, history.Items.Count);
        Assert.All(history.Items, operation => Assert.Contains(operation.Movements, movement => movement.ItemId == item.Id));
        var replacement = await CreateItem(api, new("Harina", StockItemKind.Ingredient, StockUnit.Unit, null));
        Assert.NotEqual(item.Id, replacement.Id);
    }

    [Fact]
    public async Task DeleteIngredient_UsedByCurrentRecipe_ReturnsConflict()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        var response = await api.SendWithCsrfAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/stock/items/{setup.Egg.Id}"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
