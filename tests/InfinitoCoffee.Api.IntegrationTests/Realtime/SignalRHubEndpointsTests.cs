using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InfinitoCoffee.Api.IntegrationTests.Http;
using InfinitoCoffee.Application.Orders.Dtos;
using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace InfinitoCoffee.Api.IntegrationTests.Realtime;

public sealed class SignalRHubEndpointsTests
{
    [Fact]
    public async Task PrivateHub_AnonymousNegotiate_ReturnsUnauthorized()
    {
        await using var context = new ApiTestContext();

        var response = await context.Client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Kitchen)]
    public async Task PrivateHub_AllowedRole_CanNegotiateAndConnect(UserRole role)
    {
        await using var context = new ApiTestContext();
        await context.AuthenticateAsync(role);
        await using var connection = CreateConnection(
            context.Factory,
            "/hubs/orders",
            context.CurrentAuthenticationCookie);

        var response = await context.Client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);
        await connection.StartAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task PrivateHub_CashierNegotiate_ReturnsForbidden()
    {
        await using var context = new ApiTestContext();
        await context.AuthenticateAsync(UserRole.Cashier);

        var response = await context.Client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PickupHub_AnonymousClient_CanNegotiateAndConnect()
    {
        await using var context = new ApiTestContext();
        await using var connection = CreateConnection(context.Factory, "/hubs/pickup");

        var response = await context.Client.PostAsync("/hubs/pickup/negotiate?negotiateVersion=1", null);
        await connection.StartAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task CreateOrder_EmitsFullPayloadOnlyToPrivateHub()
    {
        await using var context = new ApiTestContext();
        await context.AuthenticateAsync(UserRole.Administrator);

        await using var privateConnection = CreateConnection(context.Factory,"/hubs/orders",context.CurrentAuthenticationCookie);

        await using var pickupConnection = CreateConnection(context.Factory,"/hubs/pickup");

        var privateEvents = new List<OrderRealtimeDto>();
        var pickupEvents = new List<JsonElement>();

        privateConnection.On<OrderRealtimeDto>("OrderCreated", privateEvents.Add);
        pickupConnection.On<JsonElement>("OrderCreated", pickupEvents.Add);

        await privateConnection.StartAsync();
        await pickupConnection.StartAsync();

        var productId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedActiveProductAsync);

        var response = await context.PostAsJsonWithCsrfAsync("/api/orders", new
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

        var order = await WaitForSingleEventAsync(privateEvents);
        await Task.Delay(250);

        Assert.Equal("1", order.OrderNumber);
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Mesa 1", order.Notes);
        Assert.NotEmpty(order.Items);

        Assert.Empty(pickupEvents);
    }

    [Fact]
    public async Task StatusChange_EmitsFullPrivateAndReducedPublicPayloads()
    {
        await using var context = new ApiTestContext();
        await context.AuthenticateAsync(UserRole.Kitchen);
        await using var privateConnection = CreateConnection(
            context.Factory,
            "/hubs/orders",
            context.CurrentAuthenticationCookie);
        await using var pickupConnection = CreateConnection(context.Factory, "/hubs/pickup");
        var privateEvents = new List<OrderRealtimeDto>();
        var pickupEvents = new List<JsonElement>();
        privateConnection.On<OrderRealtimeDto>("OrderStatusChanged", privateEvents.Add);
        pickupConnection.On<JsonElement>("OrderStatusChanged", pickupEvents.Add);
        await privateConnection.StartAsync();
        await pickupConnection.StartAsync();
        var orderId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedPendingOrderAsync);

        var response = await context.PostWithCsrfAsync($"/api/orders/{orderId}/start-preparation");

        response.EnsureSuccessStatusCode();
        var privateOrder = await WaitForSingleEventAsync(privateEvents);
        var pickupOrder = await WaitForSingleEventAsync(pickupEvents);
        Assert.Equal("Preparing", privateOrder.Status);
        Assert.Equal(
            ["createdAtUtc", "id", "orderNumber", "status"],
            pickupOrder.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(orderId, pickupOrder.GetProperty("id").GetGuid());
        Assert.Equal("Preparing", pickupOrder.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_EmitsReducedPayloadToPublicHub()
    {
        await using var context = new ApiTestContext();
        await context.AuthenticateAsync(UserRole.Cashier);
        await using var pickupConnection = CreateConnection(context.Factory, "/hubs/pickup");
        var pickupEvents = new List<JsonElement>();
        pickupConnection.On<JsonElement>("OrderCancelled", pickupEvents.Add);
        await pickupConnection.StartAsync();
        var orderId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedPreparingOrderAsync);

        var response = await context.PostWithCsrfAsync($"/api/orders/{orderId}/cancel");

        response.EnsureSuccessStatusCode();
        var pickupOrder = await WaitForSingleEventAsync(pickupEvents);
        Assert.Equal(
            ["createdAtUtc", "id", "orderNumber", "status"],
            pickupOrder.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal("Cancelled", pickupOrder.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InvalidTransition_DoesNotEmitEvents()
    {
        await using var context = new ApiTestContext();
        await context.AuthenticateAsync(UserRole.Kitchen);
        await using var privateConnection = CreateConnection(
            context.Factory,
            "/hubs/orders",
            context.CurrentAuthenticationCookie);
        await using var pickupConnection = CreateConnection(context.Factory, "/hubs/pickup");
        var privateEvents = new List<OrderRealtimeDto>();
        var pickupEvents = new List<JsonElement>();
        privateConnection.On<OrderRealtimeDto>("OrderStatusChanged", privateEvents.Add);
        pickupConnection.On<JsonElement>("OrderStatusChanged", pickupEvents.Add);
        await privateConnection.StartAsync();
        await pickupConnection.StartAsync();
        var orderId = await context.ExecuteDbContextAsync(TestDataSeeder.SeedPendingOrderAsync);

        var response = await context.PostWithCsrfAsync($"/api/orders/{orderId}/deliver");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await Task.Delay(250);
        Assert.Empty(privateEvents);
        Assert.Empty(pickupEvents);
    }

    private static HubConnection CreateConnection(
        TestApiApplicationFactory factory,
        string path,
        string? authenticationCookie = null)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, path), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (authenticationCookie is not null)
                {
                    options.Headers.Add("Cookie", authenticationCookie);
                }
            })
            .WithAutomaticReconnect()
            .Build();
    }

    private static async Task<T> WaitForSingleEventAsync<T>(List<T> events)
    {
        var attempts = 0;
        while (events.Count == 0 && attempts < 20)
        {
            attempts++;
            await Task.Delay(100);
        }

        return Assert.Single(events);
    }
}
