using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Dtos;

namespace InfinitoCoffee.Application.Orders.Services;

public sealed class NullOrderEventPublisher : IOrderEventPublisher
{
    public Task OrderCreatedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OrderStatusChangedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OrderCancelledAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
