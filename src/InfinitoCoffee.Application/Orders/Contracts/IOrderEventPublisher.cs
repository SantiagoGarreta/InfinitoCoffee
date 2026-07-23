using InfinitoCoffee.Application.Orders.Dtos;

namespace InfinitoCoffee.Application.Orders.Contracts;

public interface IOrderEventPublisher
{
    Task OrderCreatedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default);

    Task OrderStatusChangedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default);

    Task OrderCancelledAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default);
}
