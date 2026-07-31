using System.Net;
using System.Net.Http.Json;
using InfinitoCoffee.Api.IntegrationTests.Http;
using InfinitoCoffee.Application.Orders.Dtos;
using InfinitoCoffee.Domain.Orders;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace InfinitoCoffee.Api.IntegrationTests.Realtime;

public sealed class SignalRHubEndpointsTests
{
    [Fact]
    public async Task NegotiateEndpoint_IsAvailable()
    {
        await using var context = new ApiTestContext();

        var response = await context.Client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HubClient_CanConnect()
    {
        await using var context = new ApiTestContext();
        await using var connection = CreateConnection(context.Factory);

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task CreateOrder_EmitsOrderCreated()
    {
        await using var context = new ApiTestContext();
        await using var connection = CreateConnection(context.Factory);
        var receivedOrders = new List<OrderRealtimeDto>();

        connection.On<OrderRealtimeDto>("OrderCreated", order => receivedOrders.Add(order));
        await connection.StartAsync();

        var productId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedActiveProductAsync);

        var response = await context.Client.PostAsJsonAsync("/api/orders", new
        {
            source = "Counter",
            notes = "Mesa 1",
            items = new[]
            {
                new
                {
                    productId,
                    quantity = 1,
                    notes = "Sin canela"
                }
            }
        });

        response.EnsureSuccessStatusCode();

        var order = await WaitForSingleEventAsync(receivedOrders);
        Assert.Equal("1", order.OrderNumber);
        Assert.Equal("Pending", order.Status);
    }

    [Fact]
    public async Task StartPreparation_EmitsOrderStatusChanged()
    {
        await using var context = new ApiTestContext();
        await using var connection = CreateConnection(context.Factory);
        var receivedOrders = new List<OrderRealtimeDto>();

        connection.On<OrderRealtimeDto>("OrderStatusChanged", order => receivedOrders.Add(order));
        await connection.StartAsync();

        var orderId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedPendingOrderAsync);

        var response = await context.Client.PostAsync($"/api/orders/{orderId}/start-preparation", content: null);

        response.EnsureSuccessStatusCode();

        var order = await WaitForSingleEventAsync(receivedOrders);
        Assert.Equal(orderId, order.Id);
        Assert.Equal("Preparing", order.Status);
    }

    [Fact]
    public async Task Cancel_EmitsOrderCancelled()
    {
        await using var context = new ApiTestContext();
        await using var connection = CreateConnection(context.Factory);
        var receivedOrders = new List<OrderRealtimeDto>();

        connection.On<OrderRealtimeDto>("OrderCancelled", order => receivedOrders.Add(order));
        await connection.StartAsync();

        var orderId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedPreparingOrderAsync);

        var response = await context.Client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        response.EnsureSuccessStatusCode();

        var order = await WaitForSingleEventAsync(receivedOrders);
        Assert.Equal(orderId, order.Id);
        Assert.Equal("Cancelled", order.Status);
        Assert.NotNull(order.CancelledAtUtc);
    }

    [Fact]
    public async Task InvalidTransition_DoesNotEmitEvents()
    {
        await using var context = new ApiTestContext();
        await using var connection = CreateConnection(context.Factory);
        var receivedOrders = new List<OrderRealtimeDto>();

        connection.On<OrderRealtimeDto>("OrderStatusChanged", order => receivedOrders.Add(order));
        await connection.StartAsync();

        var orderId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedPendingOrderAsync);

        var response = await context.Client.PostAsync($"/api/orders/{orderId}/deliver", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await Task.Delay(250);
        Assert.Empty(receivedOrders);
    }

    private static HubConnection CreateConnection(TestApiApplicationFactory factory)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, "/hubs/orders"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    private static async Task<OrderRealtimeDto> WaitForSingleEventAsync(List<OrderRealtimeDto> receivedOrders)
    {
        var attempts = 0;

        while (receivedOrders.Count == 0 && attempts < 20)
        {
            attempts++;
            await Task.Delay(100);
        }

        return Assert.Single(receivedOrders);
    }
}
