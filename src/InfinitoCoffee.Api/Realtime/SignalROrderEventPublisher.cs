using InfinitoCoffee.Api.Contracts;
using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

public sealed class SignalROrderEventPublisher : IOrderEventPublisher
{
    private readonly IHubContext<OrdersHub, IOrdersClient> _ordersHubContext;
    private readonly IHubContext<PickupHub, IPickupClient> _pickupHubContext;
    private readonly ILogger<SignalROrderEventPublisher> _logger;

    public SignalROrderEventPublisher(
        IHubContext<OrdersHub, IOrdersClient> ordersHubContext,
        IHubContext<PickupHub, IPickupClient> pickupHubContext,
        ILogger<SignalROrderEventPublisher> logger)
    {
        _ordersHubContext = ordersHubContext;
        _pickupHubContext = pickupHubContext;
        _logger = logger;
    }

    public Task OrderCreatedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return PublishOrdersAsync("OrderCreated", order, client => client.OrderCreated(order), cancellationToken);
    }

    public Task OrderStatusChangedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return PublishToBothHubsAsync(
            "OrderStatusChanged",
            order,
            client => client.OrderStatusChanged(order),
            client => client.OrderStatusChanged(ApiContractMapper.MapPickupOrder(order)),
            cancellationToken);
    }

    public Task OrderCancelledAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return PublishToBothHubsAsync(
            "OrderCancelled",
            order,
            client => client.OrderCancelled(order),
            client => client.OrderCancelled(ApiContractMapper.MapPickupOrder(order)),
            cancellationToken);
    }

    private async Task PublishToBothHubsAsync(
        string eventName,
        OrderRealtimeDto order,
        Func<IOrdersClient, Task> publishOrdersAction,
        Func<IPickupClient, Task> publishPickupAction,
        CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            PublishOrdersAsync(eventName, order, publishOrdersAction, cancellationToken),
            PublishPickupAsync(eventName, order, publishPickupAction, cancellationToken));
    }

    private Task PublishOrdersAsync(
        string eventName,
        OrderRealtimeDto order,
        Func<IOrdersClient, Task> publishAction,
        CancellationToken cancellationToken)
    {
        return PublishAsync(
            eventName,
            order,
            "private orders",
            () => publishAction(_ordersHubContext.Clients.All),
            cancellationToken);
    }

    private Task PublishPickupAsync(
        string eventName,
        OrderRealtimeDto order,
        Func<IPickupClient, Task> publishAction,
        CancellationToken cancellationToken)
    {
        return PublishAsync(
            eventName,
            order,
            "public pickup",
            () => publishAction(_pickupHubContext.Clients.All),
            cancellationToken);
    }

    private async Task PublishAsync(
        string eventName,
        OrderRealtimeDto order,
        string hubName,
        Func<Task> publishAction,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await publishAction();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "SignalR publication failed for {EventName} on order {OrderId} in the {HubName} hub. The persisted state remains the source of truth and clients must resynchronize.",
                eventName,
                order.Id,
                hubName);
        }
    }
}
