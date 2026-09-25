using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class StockEndpointsTests
{
    [Fact]
    public async Task ProductionWithDiscard_ConsumesWholeRecipeButAddsOnlyUsableProducts()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Receive(api, setup.Egg.Id, 20);
        var request = new ProductionRequest(Guid.NewGuid(), setup.Recipe.Id, 20, StockLocation.Factory, "Dos piezas quemadas", 2);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/production", request)).EnsureSuccessStatusCode();
        Assert.Equal(10, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        Assert.Equal(18, await Quantity(api, setup.Scone.Id, StockLocation.Factory));
        var history = await Read<StockHistoryDto>(await api.Client.GetAsync("/api/stock/history"));
        var operation = Assert.Single(history.Items, x => x.Type == "Production");
        Assert.Contains(operation.Movements, x => x.ItemId == setup.Scone.Id && x.Delta == -2);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/production",
            request with { OperationId = Guid.NewGuid(), Notes = "" })).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Kitchen)]
    public async Task EveryStockEndpoint_IsRestrictedToAdministrators(UserRole role)
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(role);
        foreach (var path in new[] { "/api/stock", "/api/stock/transfers", "/api/stock/history" })
            Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync(path)).StatusCode);
        foreach (var path in new[] { "items", "recipes", "receipts", "production", "transfers", "waste", "counts", $"transfers/{Guid.NewGuid()}/receive" })
            Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonWithCsrfAsync($"/api/stock/{path}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PutAsJsonWithCsrfAsync($"/api/stock/items/{Guid.NewGuid()}", new { })).StatusCode);
    }

    [Fact]
    public async Task AnonymousVisitor_CannotReadStock()
    {
        await using var api = new ApiTestContext();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/api/stock")).StatusCode);
    }

    [Fact]
    public async Task EggsToScones_TracksProductionTransitReceiptAndAutomaticSale()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Receive(api, setup.Egg.Id, 20);
        var production = new ProductionRequest(Guid.NewGuid(), setup.Recipe.Id, 20, StockLocation.Factory, "Tanda mañana");
        (await api.PostAsJsonWithCsrfAsync("/api/stock/production", production)).EnsureSuccessStatusCode();
        Assert.Equal(10, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        Assert.Equal(20, await Quantity(api, setup.Scone.Id, StockLocation.Factory));

        var transfer = new TransferRequest(Guid.NewGuid(), StockLocation.Factory, StockLocation.Cafe, "Envío mañana", [new(setup.Scone.Id, 20, "unit")]);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/transfers", transfer)).EnsureSuccessStatusCode();
        Assert.Equal(0, await Quantity(api, setup.Scone.Id, StockLocation.Factory));
        Assert.Equal(20, await Quantity(api, setup.Scone.Id, StockLocation.Transit));
        Assert.Equal(0, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));

        var reception = new ReceiveTransferRequest(Guid.NewGuid(), "Llegó completo", [new(setup.Scone.Id, 20, "unit")]);
        (await api.PostAsJsonWithCsrfAsync($"/api/stock/transfers/{transfer.OperationId}/receive", reception)).EnsureSuccessStatusCode();
        (await api.PostAsJsonWithCsrfAsync($"/api/stock/transfers/{transfer.OperationId}/receive", reception)).EnsureSuccessStatusCode();
        Assert.Equal(20, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));
        Assert.Equal(0, await Quantity(api, setup.Scone.Id, StockLocation.Transit));

        var orderId = await ReadyOrder(api, setup.Scone.ProductId!.Value, 3);
        (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).EnsureSuccessStatusCode();
        Assert.Equal(17, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).StatusCode);
        Assert.Equal(17, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));
        Assert.Equal(10, await Quantity(api, setup.Egg.Id, StockLocation.Factory));

        var history = await Read<StockHistoryDto>(await api.Client.GetAsync("/api/stock/history"));
        Assert.Equal(5, history.Items.Count);
        Assert.Single(history.Items, x => x.Type == "Sale" && x.OrderId == orderId);
        var productionAudit = Assert.Single(history.Items, x => x.Type == "Production");
        Assert.Equal(api.CurrentUserId, productionAudit.ActorId);
        Assert.Equal(setup.Recipe.Id, productionAudit.RecipeId);
        Assert.Contains(productionAudit.Movements, x => x.ItemId == setup.Egg.Id && x.Before == 20 && x.Delta == -10 && x.After == 10);
    }

    [Fact]
    public async Task InsufficientIngredient_RollsBackEveryConsumptionAndOutput()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        var flour = await CreateItem(api, new("Harina", StockItemKind.Ingredient, StockUnit.Gram, null));
        await Receive(api, setup.Egg.Id, 20);
        var recipe = await SaveRecipe(api, new(setup.Scone.Id, 10, [new(setup.Egg.Id, 5, "unit"), new(flour.Id, 1, "kg")]));
        var response = await api.PostAsJsonWithCsrfAsync("/api/stock/production", new ProductionRequest(Guid.NewGuid(), recipe.Id, 10, StockLocation.Factory, null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(20, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        Assert.Equal(0, await Quantity(api, setup.Scone.Id, StockLocation.Factory));
        Assert.Equal(0, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(x => x.Type == "Production")));
    }

    [Fact]
    public async Task Retry_IsIdempotentAndRejectsReusedIdWithDifferentPayload()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        var receipt = new ReceiptRequest(Guid.NewGuid(), StockLocation.Factory, "Factura", [new(setup.Egg.Id, 20, "unit")]);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts", receipt)).EnsureSuccessStatusCode();
        (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts", receipt)).EnsureSuccessStatusCode();
        Assert.Equal(20, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts", receipt with { Notes = "Otra factura" })).StatusCode);
        Assert.Equal(1, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync()));
    }

    [Fact]
    public async Task UnitsAreConverted_AndIncompatibleOrFractionalUnitsAreRejected()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        var flour = await CreateItem(api, new("Harina", StockItemKind.Ingredient, StockUnit.Gram, null));
        var milk = await CreateItem(api, new("Leche", StockItemKind.Ingredient, StockUnit.Milliliter, null));
        var eggs = await CreateItem(api, new("Huevos", StockItemKind.Ingredient, StockUnit.Unit, null));
        var receipt = new ReceiptRequest(Guid.NewGuid(), StockLocation.Factory, null, [new(flour.Id, 1.25m, "kg"), new(milk.Id, 2m, "l")]);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts", receipt)).EnsureSuccessStatusCode();
        Assert.Equal(1250, await Quantity(api, flour.Id, StockLocation.Factory));
        Assert.Equal(2000, await Quantity(api, milk.Id, StockLocation.Factory));
        foreach (var line in new StockLineInput[] { new(eggs.Id, 0.5m, "unit"), new(flour.Id, 1m, "l"), new(flour.Id, -1m, "kg"), new(flour.Id, 0.0001m, "g") })
            Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts", new ReceiptRequest(Guid.NewGuid(), StockLocation.Factory, null, [line]))).StatusCode);
    }

    [Fact]
    public async Task CountsRequireReasonAndFreshBalance_AndKeepTheDifferenceInHistory()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Receive(api, setup.Egg.Id, 20);
        var dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        var balance = dashboard.Balances.Single(x => x.ItemId == setup.Egg.Id && x.Location == StockLocation.Factory);
        var count = new CountRequest(Guid.NewGuid(), StockLocation.Factory, "Conteo de cierre", [new(setup.Egg.Id, 18, "unit", balance.Revision)]);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/counts", count with { Notes = "" })).StatusCode);
        await Receive(api, setup.Egg.Id, 1);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/counts", count)).StatusCode);
        Assert.Equal(21, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        balance = dashboard.Balances.Single(x => x.ItemId == setup.Egg.Id && x.Location == StockLocation.Factory);
        count = count with { Lines = [new(setup.Egg.Id, 18, "unit", balance.Revision)] };
        (await api.PostAsJsonWithCsrfAsync("/api/stock/counts", count)).EnsureSuccessStatusCode();
        Assert.Equal(18, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        var history = await Read<StockHistoryDto>(await api.Client.GetAsync($"/api/stock/history?itemId={setup.Egg.Id}&location=Factory"));
        var entry = Assert.Single(history.Items, x => x.Type == "Count");
        var movement = Assert.Single(entry.Movements);
        Assert.Equal(-3, movement.Delta);
        Assert.Equal(21, movement.Before);
        Assert.Equal("Conteo de cierre", entry.Notes);
    }

    [Fact]
    public async Task TransferShortfall_RequiresExplanationAndCannotBeReceivedTwice()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Receive(api, setup.Scone.Id, 20);
        var transfer = new TransferRequest(Guid.NewGuid(), StockLocation.Factory, StockLocation.Cafe, null, [new(setup.Scone.Id, 20, "unit")]);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/transfers", transfer)).EnsureSuccessStatusCode();
        var receipt = new ReceiveTransferRequest(Guid.NewGuid(), null, [new(setup.Scone.Id, 18, "unit")]);
        var path = $"/api/stock/transfers/{transfer.OperationId}/receive";
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync(path, receipt)).StatusCode);
        Assert.Equal(20, await Quantity(api, setup.Scone.Id, StockLocation.Transit));
        receipt = receipt with { Notes = "Dos scones dañados en el traslado" };
        (await api.PostAsJsonWithCsrfAsync(path, receipt)).EnsureSuccessStatusCode();
        Assert.Equal(18, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));
        Assert.Equal(0, await Quantity(api, setup.Scone.Id, StockLocation.Transit));
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync(path, receipt with { OperationId = Guid.NewGuid() })).StatusCode);
        var transfers = await Read<TransferDto[]>(await api.Client.GetAsync("/api/stock/transfers"));
        Assert.Equal(2, transfers.Single().Lines.Single().Sent - transfers.Single().Lines.Single().Received);
    }

    [Fact]
    public async Task RecipeRevision_DoesNotAlterPreviousRecipeOrConsumption()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Receive(api, setup.Egg.Id, 20);
        var newer = await SaveRecipe(api, new(setup.Scone.Id, 10, [new(setup.Egg.Id, 6, "unit")]));
        Assert.Equal(2, newer.Version);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/production", new ProductionRequest(Guid.NewGuid(), setup.Recipe.Id, 10, StockLocation.Factory, "Receta original"))).EnsureSuccessStatusCode();
        Assert.Equal(15, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        (await api.PostAsJsonWithCsrfAsync("/api/stock/production", new ProductionRequest(Guid.NewGuid(), newer.Id, 10, StockLocation.Factory, "Receta nueva"))).EnsureSuccessStatusCode();
        Assert.Equal(9, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
        var dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.Equal(newer.Id, Assert.Single(dashboard.Recipes).Id);
    }

    [Fact]
    public async Task InsufficientCafeStock_DoesNotDeliverOrderOrConsumeOtherItems()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        var orderId = await ReadyOrder(api, setup.Scone.ProductId!.Value, 3);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver")).StatusCode);
        Assert.Equal(OrderStatus.Ready, await api.ExecuteDbContextAsync(async db => (await db.Orders.SingleAsync(x => x.Id == orderId)).Status));
        Assert.Equal(0, await api.ExecuteDbContextAsync(db => db.StockOperations.CountAsync(x => x.Type == "Sale")));
    }

    [Fact]
    public async Task CancellationDoesNotConsumeStock_AndWasteNeedsAReason()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        await Receive(api, setup.Scone.Id, 10, StockLocation.Cafe);
        var orderId = await ReadyOrder(api, setup.Scone.ProductId!.Value, 3);
        (await api.PostWithCsrfAsync($"/api/orders/{orderId}/cancel")).EnsureSuccessStatusCode();
        Assert.Equal(10, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));
        var waste = new WasteRequest(Guid.NewGuid(), StockLocation.Cafe, "", [new(setup.Scone.Id, 2, "unit")]);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/waste", waste)).StatusCode);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/waste", waste with { Notes = "Vencimiento" })).EnsureSuccessStatusCode();
        Assert.Equal(8, await Quantity(api, setup.Scone.Id, StockLocation.Cafe));
    }

    [Fact]
    public async Task InvalidRecipesAndDuplicateLinesCannotChangeStock()
    {
        await using var api = new ApiTestContext();
        var setup = await Setup(api);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/recipes",
            new RecipeRequest(setup.Egg.Id, 10, [new(setup.Egg.Id, 5, "unit")]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/recipes",
            new RecipeRequest(setup.Scone.Id, 10, [new(setup.Scone.Id, 5, "unit")]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts",
            new ReceiptRequest(Guid.NewGuid(), StockLocation.Factory, null, [new(setup.Egg.Id, 5, "unit"), new(setup.Egg.Id, 5, "unit")]))).StatusCode);
        Assert.Equal(0, await Quantity(api, setup.Egg.Id, StockLocation.Factory));
    }

    private static async Task<(StockItemDto Egg, StockItemDto Scone, RecipeDto Recipe)> Setup(ApiTestContext api)
    {
        await api.AuthenticateAsync();
        var productId = await api.ExecuteDbContextAsync(async db =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(db, "Panadería");
            return (await TestDataSeeder.AddProductAsync(db, category.Id, "Scone")).Id;
        });
        var egg = await CreateItem(api, new("Huevos", StockItemKind.Ingredient, StockUnit.Unit, null));
        var scone = await CreateItem(api, new("Scones", StockItemKind.FinishedProduct, StockUnit.Unit, productId));
        var recipe = await SaveRecipe(api, new(scone.Id, 10, [new(egg.Id, 5, "unit")]));
        return (egg, scone, recipe);
    }

    private static async Task<StockItemDto> CreateItem(ApiTestContext api, StockItemRequest request)
    {
        var response = await api.PostAsJsonWithCsrfAsync("/api/stock/items", request);
        response.EnsureSuccessStatusCode();
        return await Read<StockItemDto>(response);
    }

    private static async Task<RecipeDto> SaveRecipe(ApiTestContext api, RecipeRequest request)
    {
        var response = await api.PostAsJsonWithCsrfAsync("/api/stock/recipes", request);
        response.EnsureSuccessStatusCode();
        return await Read<RecipeDto>(response);
    }

    private static async Task Receive(ApiTestContext api, Guid id, decimal quantity, StockLocation location = StockLocation.Factory) =>
        (await api.PostAsJsonWithCsrfAsync("/api/stock/receipts", new ReceiptRequest(Guid.NewGuid(), location, "Ingreso", [new(id, quantity, "unit")]))).EnsureSuccessStatusCode();

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        }))!;
    }

    private static Task<decimal> Quantity(ApiTestContext api, Guid id, StockLocation location) =>
        api.ExecuteDbContextAsync(async db => (await db.StockBalances.SingleAsync(x => x.ItemId == id && x.Location == location)).Quantity);

    private static Task<Guid> ReadyOrder(ApiTestContext api, Guid productId, int quantity) => api.ExecuteDbContextAsync(async db =>
    {
        var order = new Order("1", DateTime.UtcNow, [new OrderItem(productId, "Scone", 50m, 10m, quantity)]);
        order.StartPreparing(DateTime.UtcNow);
        order.MarkReady(DateTime.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    });
}
