using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class BranchEndpointsTests
{
    [Fact]
    public async Task BothBranchesConsumeSharedIngredientsButOwnFinishedProductsAndSales()
    {
        await using var api = new ApiTestContext();
        var (egg, scone) = await Setup(api);
        await Adjust(api, egg.Id, 30);
        await Adjust(api, scone.Id, 10);
        var first = await Sell(api, scone.ProductId!.Value, 2);
        Select(api, 2);
        await Adjust(api, scone.Id, 10);
        var second = await Sell(api, scone.ProductId!.Value, 3);

        Assert.Equal(1, first.BranchId);
        Assert.Equal(2, second.BranchId);
        Assert.Equal("1", first.OrderNumber);
        Assert.Equal("1", second.OrderNumber);
        var dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.Equal(20, Assert.Single(dashboard.Balances, x => x.ItemId == egg.Id).Quantity);
        Assert.Equal(7, Assert.Single(dashboard.Balances, x => x.ItemId == scone.Id).Quantity);
        var secondResults = await Read<OrderResultsResponse>(await api.Client.GetAsync("/api/orders/summary"));
        Assert.Equal(150, secondResults.CurrentPeriod.TotalRevenue);
        Assert.Equal(120, secondResults.CurrentPeriod.TotalProfit);
        Assert.Equal(1, secondResults.OperationalSnapshot.TotalOrdersCount);

        Select(api, 1);
        dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.Equal(20, Assert.Single(dashboard.Balances, x => x.ItemId == egg.Id).Quantity);
        Assert.Equal(8, Assert.Single(dashboard.Balances, x => x.ItemId == scone.Id).Quantity);
        var firstResults = await Read<OrderResultsResponse>(await api.Client.GetAsync("/api/orders/summary"));
        Assert.Equal(100, firstResults.CurrentPeriod.TotalRevenue);
        Assert.Equal(80, firstResults.CurrentPeriod.TotalProfit);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/api/orders/{second.Id}")).StatusCode);
    }

    [Fact]
    public async Task ManualTransferDoesNotConsumeIngredientsAgainAndHistoryShowsItsScope()
    {
        await using var api = new ApiTestContext();
        var (egg, scone) = await Setup(api);
        await Adjust(api, egg.Id, 30);
        await Adjust(api, scone.Id, 10);
        await Adjust(api, scone.Id, -4, false);
        Select(api, 2);
        var receipt = new StockAdjustmentRequest(Guid.NewGuid(), "Traslado de sucursal 1", [new(scone.Id, 4, "unit")], false);
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", receipt)).EnsureSuccessStatusCode();
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", receipt)).EnsureSuccessStatusCode();
        var dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.Equal(25, Assert.Single(dashboard.Balances, x => x.ItemId == egg.Id).Quantity);
        Assert.Equal(4, Assert.Single(dashboard.Balances, x => x.ItemId == scone.Id).Quantity);
        Select(api, 1);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments", receipt)).StatusCode);
        var history = await Read<StockHistoryDto>(await api.Client.GetAsync("/api/stock/history"));
        Assert.DoesNotContain(history.Items, x => x.Id == receipt.OperationId);
        Assert.DoesNotContain(history.Items.SelectMany(x => x.Movements), x => x.BranchId == 2);
        dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.Equal(6, Assert.Single(dashboard.Balances, x => x.ItemId == scone.Id).Quantity);
    }

    [Fact]
    public async Task SecondBranchCannotUseFirstBranchesFinishedStockAndFailedProductionRollsBack()
    {
        await using var api = new ApiTestContext();
        var (egg, scone) = await Setup(api);
        await Adjust(api, egg.Id, 5);
        await Adjust(api, scone.Id, 10);
        Select(api, 2);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/orders", Order(scone.ProductId!.Value, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), "Producción", [new(scone.Id, 10, "unit")]))).StatusCode);
        Assert.Equal(0, await api.ExecuteDbContextAsync(db => db.Orders.CountAsync()));
        var dashboard = await Read<StockDashboardDto>(await api.Client.GetAsync("/api/stock"));
        Assert.Equal(0, Assert.Single(dashboard.Balances, x => x.ItemId == scone.Id).Quantity);
        Assert.Equal(0, Assert.Single(dashboard.Balances, x => x.ItemId == egg.Id).Quantity);
    }

    [Theory]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Kitchen)]
    public async Task OperationalUsersCannotReadOrMutateOtherBranchOrders(UserRole role)
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(role);
        var otherId = await api.ExecuteDbContextAsync(async db =>
        {
            var order = new InfinitoCoffee.Domain.Orders.Order("1", DateTime.UtcNow,
                [new InfinitoCoffee.Domain.Orders.OrderItem(Guid.NewGuid(), "Scone", 50m, 10m, 1)], branchId: 2);
            db.Orders.Add(order); await db.SaveChangesAsync(); return order.Id;
        });
        Assert.Empty(await Read<OrderResponse[]>(await api.Client.GetAsync("/api/orders/active")));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/api/orders/{otherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostWithCsrfAsync($"/api/orders/{otherId}/start-preparation")).StatusCode);
        Select(api, 2);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/orders/active")).StatusCode);
    }

    [Fact]
    public async Task AssignedBranchAppliesWithoutHeaderAndPickupIsIndependent()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        // The assignment is read from persistence on each operational request.
        await api.ExecuteDbContextAsync(async db =>
        {
            (await db.Users.SingleAsync(x => x.Id == api.CurrentUserId)).AssignBranch(2);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync("/api/orders/active")).StatusCode);
        await api.GetCsrfTokenAsync();
        (await api.PostAsJsonWithCsrfAsync("/api/auth/login", new { username = "test.kitchen", password = "Correct_password!" })).EnsureSuccessStatusCode();
        await api.GetCsrfTokenAsync();
        var productId = await api.ExecuteDbContextAsync(TestDataSeeder.SeedActiveProductAsync);
        var ids = await api.ExecuteDbContextAsync(async db =>
        {
            var first = new InfinitoCoffee.Domain.Orders.Order("1", DateTime.UtcNow,
                [new InfinitoCoffee.Domain.Orders.OrderItem(productId, "Café", 50m, 10m, 1)], branchId: 1);
            var second = new InfinitoCoffee.Domain.Orders.Order("1", DateTime.UtcNow,
                [new InfinitoCoffee.Domain.Orders.OrderItem(productId, "Café", 50m, 10m, 1)], branchId: 2);
            first.StartPreparing(DateTime.UtcNow); second.StartPreparing(DateTime.UtcNow);
            db.Orders.AddRange(first, second); await db.SaveChangesAsync(); return (first.Id, second.Id);
        });
        Assert.Equal(ids.Item2, Assert.Single(await Read<OrderResponse[]>(await api.Client.GetAsync("/api/orders/active"))).Id);
        Assert.Equal(ids.Item1, Assert.Single(await Read<PickupOrderResponse[]>(await api.Client.GetAsync("/api/orders/pickup?branchId=1"))).Id);
        Assert.Equal(ids.Item2, Assert.Single(await Read<PickupOrderResponse[]>(await api.Client.GetAsync("/api/orders/pickup?branchId=2"))).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.GetAsync("/api/orders/pickup?branchId=99")).StatusCode);
    }

    [Fact]
    public async Task AdministratorCanRenameBranchesAndAssignNewUserToSecondBranch()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync();
        (await api.PutAsJsonWithCsrfAsync("/api/branches/2", new { name = "Local de la esquina" })).EnsureSuccessStatusCode();
        var branches = await Read<JsonElement>(await api.Client.GetAsync("/api/branches"));
        Assert.Equal("Local de la esquina", branches[1].GetProperty("name").GetString());
        var response = await api.PostAsJsonWithCsrfAsync("/api/users", new
        { username = "caja.dos", displayName = "Caja dos", password = "Correct_password!", role = "Cashier", branchId = 2 });
        Assert.Equal(2, (await Read<JsonElement>(response)).GetProperty("branchId").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsJsonWithCsrfAsync("/api/users", new
        { username = "caja.invalida", displayName = "Caja", password = "Correct_password!", role = "Cashier", branchId = 99 })).StatusCode);
    }

    private static async Task<(StockItemDto, StockItemDto)> Setup(ApiTestContext api)
    {
        await api.AuthenticateAsync();
        var productId = await api.ExecuteDbContextAsync(async db =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(db, "Cantina");
            return (await TestDataSeeder.AddProductAsync(db, category.Id, "Scone", 50m, 10m)).Id;
        });
        var egg = await Read<StockItemDto>(await api.PostAsJsonWithCsrfAsync("/api/stock/items",
            new StockItemRequest("Huevos", StockItemKind.Ingredient, StockUnit.Unit, null)));
        var scone = await Read<StockItemDto>(await api.PostAsJsonWithCsrfAsync("/api/stock/items",
            new StockItemRequest("Scones", StockItemKind.FinishedProduct, StockUnit.Unit, productId)));
        (await api.PostAsJsonWithCsrfAsync("/api/stock/recipes", new RecipeRequest(scone.Id, 10, [new(egg.Id, 5, "unit")]))).EnsureSuccessStatusCode();
        return (egg, scone);
    }
    private static CreateOrderRequest Order(Guid productId, int quantity) => new()
    { Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = quantity }] };
    private static async Task<OrderResponse> Sell(ApiTestContext api, Guid productId, int quantity) =>
        await Read<OrderResponse>(await api.PostAsJsonWithCsrfAsync("/api/orders", Order(productId, quantity)));
    private static async Task Adjust(ApiTestContext api, Guid itemId, decimal delta, bool consume = true) =>
        (await api.PostAsJsonWithCsrfAsync("/api/stock/adjustments",
            new StockAdjustmentRequest(Guid.NewGuid(), "Registro de stock", [new(itemId, delta, "unit")], consume))).EnsureSuccessStatusCode();
    private static void Select(ApiTestContext api, int branchId)
    {
        api.Client.DefaultRequestHeaders.Remove("X-Branch-Id");
        api.Client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } }))!;
    }
}
