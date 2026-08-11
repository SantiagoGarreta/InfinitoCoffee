using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

public sealed class OrderEndpointsTests
{
    [Fact]
    public async Task CreateOrder_WhenValid_ReturnsCreatedWithLocation()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);
        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            return (await TestDataSeeder.AddProductAsync(dbContext, category.Id)).Id;
        });

        var response = await api.PostAsJsonWithCsrfAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                Source = "Counter",
                Notes = "Sin azucar",
                Items =
                [
                    new CreateOrderItemRequest
                    {
                        ProductId = productId,
                        Quantity = 2,
                        Notes = "Uno sin canela"
                    }
                ]
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/orders/", response.Headers.Location!.ToString(), StringComparison.Ordinal);

        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("1", order.OrderNumber);
        Assert.Equal("Pending", order.Status);
        Assert.Single(order.Items);
    }

    [Fact]
    public async Task CreateOrder_WithMissingProduct_ReturnsProblemDetails404()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.PostAsJsonWithCsrfAsync(
                "/api/orders",
                new CreateOrderRequest
                {
                    Source = "Counter",
                    Items = [new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }]
                }),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateOrder_WithInactiveProduct_ReturnsProblemDetails400()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);
        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            return (await TestDataSeeder.AddProductAsync(dbContext, category.Id, isActive: false)).Id;
        });

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.PostAsJsonWithCsrfAsync(
                "/api/orders",
                new CreateOrderRequest
                {
                    Source = "Counter",
                    Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
                }),
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WhenOrdersAlreadyExist_AssignsNextDisplayOrderNumber()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);
        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            return (await TestDataSeeder.AddProductAsync(dbContext, category.Id)).Id;
        });

        await api.PostAsJsonWithCsrfAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                Source = "Counter",
                Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
            });

        var response = await api.PostAsJsonWithCsrfAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                Source = "Counter",
                Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = 1 }]
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("2", order.OrderNumber);
    }

    [Fact]
    public async Task CreateOrder_WhenLatestDisplayOrderNumberIs99_WrapsTo1()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);

        var productId = await api.ExecuteDbContextAsync(async dbContext =>
        {
            var category = await TestDataSeeder.AddCategoryAsync(dbContext);
            var product = await TestDataSeeder.AddProductAsync(dbContext, category.Id);
            await TestDataSeeder.AddOrderAsync(
                dbContext,
                "99",
                OrderStatus.Pending,
                DateTime.UtcNow.AddMinutes(-1));

            return product.Id;
        });

        var response = await api.PostAsJsonWithCsrfAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                Source = "Counter",
                Items =
                [
                    new CreateOrderItemRequest
                    {
                        ProductId = productId,
                        Quantity = 1
                    }
                ]
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("1", order.OrderNumber);
    }

    [Fact]
    public async Task GetOrder_WhenExists_ReturnsOk()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Pending, DateTime.UtcNow.AddMinutes(-5))).Id);

        var response = await api.Client.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal(orderId, order.Id);
    }

    [Fact]
    public async Task GetOrder_WhenMissing_ReturnsProblemDetails404()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.Client.GetAsync($"/api/orders/{Guid.NewGuid()}"),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetActive_ReturnsOnlyActiveOrders()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        await api.ExecuteDbContextAsync(async dbContext =>
        {
            var now = DateTime.UtcNow;
            await TestDataSeeder.AddOrderAsync(dbContext, "A-100", OrderStatus.Pending, now.AddMinutes(-6));
            await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Delivered, now.AddMinutes(-5));
            await TestDataSeeder.AddOrderAsync(dbContext, "A-102", OrderStatus.Cancelled, now.AddMinutes(-4));
        });

        var response = await api.Client.GetAsync("/api/orders/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await api.ReadRequiredAsync<IReadOnlyCollection<OrderResponse>>(response);
        Assert.Single(orders);
        Assert.Equal("A-100", orders.Single().OrderNumber);
    }

    [Fact]
    public async Task GetPickup_FiltersOrdersCorrectly()
    {
        await using var api = new ApiTestContext();
        await api.ExecuteDbContextAsync(async dbContext =>
        {
            var now = DateTime.UtcNow;
            await TestDataSeeder.AddOrderAsync(dbContext, "A-100", OrderStatus.Preparing, now.AddMinutes(-10));
            await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Ready, now.AddMinutes(-12), now.AddMinutes(-5));
            await TestDataSeeder.AddOrderAsync(dbContext, "A-102", OrderStatus.Ready, now.AddMinutes(-40), now.AddMinutes(-20));
            await TestDataSeeder.AddOrderAsync(dbContext, "A-103", OrderStatus.Delivered, now.AddMinutes(-15), now.AddMinutes(-10));
        });

        var response = await api.Client.GetAsync("/api/orders/pickup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await api.ReadRequiredAsync<IReadOnlyCollection<PickupOrderResponse>>(response);
        var orderNumbers = orders.Select(order => order.OrderNumber).ToArray();
        Assert.Equal(2, orderNumbers.Length);
        Assert.Contains("A-100", orderNumbers);
        Assert.Contains("A-101", orderNumbers);
    }

    [Fact]
    public async Task StartPreparation_ReturnsUpdatedOrder()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Pending, DateTime.UtcNow.AddMinutes(-2))).Id);

        var response = await api.PostWithCsrfAsync($"/api/orders/{orderId}/start-preparation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("Preparing", order.Status);
    }

    [Fact]
    public async Task MarkReady_ReturnsUpdatedOrder()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Preparing, DateTime.UtcNow.AddMinutes(-3))).Id);

        var response = await api.PostWithCsrfAsync($"/api/orders/{orderId}/mark-ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("Ready", order.Status);
    }

    [Fact]
    public async Task Deliver_ReturnsUpdatedOrder()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Ready, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1))).Id);

        var response = await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("Delivered", order.Status);
    }

    [Fact]
    public async Task Cancel_ReturnsUpdatedOrder()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Cashier);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Pending, DateTime.UtcNow.AddMinutes(-2))).Id);

        var response = await api.PostWithCsrfAsync($"/api/orders/{orderId}/cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await api.ReadRequiredAsync<OrderResponse>(response);
        Assert.Equal("Cancelled", order.Status);
    }

    [Fact]
    public async Task InvalidTransition_ReturnsProblemDetails400()
    {
        await using var api = new ApiTestContext();
        await api.AuthenticateAsync(UserRole.Kitchen);
        var orderId = await api.ExecuteDbContextAsync(async dbContext =>
            (await TestDataSeeder.AddOrderAsync(dbContext, "A-101", OrderStatus.Pending, DateTime.UtcNow.AddMinutes(-2))).Id);

        await HttpProblemDetailsAssertions.AssertProblemDetailsAsync(
            await api.PostWithCsrfAsync($"/api/orders/{orderId}/deliver"),
            HttpStatusCode.BadRequest);
    }
}
