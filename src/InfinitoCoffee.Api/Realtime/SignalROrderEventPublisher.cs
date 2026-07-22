using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

public sealed class SignalROrderEventPublisher : IOrderEventPublisher
{
    private readonly IHubContext<OrdersHub, IOrdersClient> _hubContext;
    private readonly ILogger<SignalROrderEventPublisher> _logger;

    public SignalROrderEventPublisher(
        IHubContext<OrdersHub, IOrdersClient> hubContext,
        ILogger<SignalROrderEventPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task OrderCreatedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return PublishAsync("OrderCreated", order, client => client.OrderCreated(order), cancellationToken);
    }

    public Task OrderStatusChangedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return PublishAsync("OrderStatusChanged", order, client => client.OrderStatusChanged(order), cancellationToken);
    }

    public Task OrderCancelledAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return PublishAsync("OrderCancelled", order, client => client.OrderCancelled(order), cancellationToken);
    }

    private async Task PublishAsync(
        string eventName,
        OrderRealtimeDto order,
        Func<IOrdersClient, Task> publishAction,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await publishAction(_hubContext.Clients.All);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "SignalR publication failed for {EventName} on order {OrderId}. The persisted state remains the source of truth and clients must resynchronize.",
                eventName,
                order.Id);
        }
    }
}
