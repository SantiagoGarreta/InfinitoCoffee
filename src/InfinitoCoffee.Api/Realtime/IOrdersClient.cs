using InfinitoCoffee.Application.Orders.Dtos;

namespace InfinitoCoffee.Api.Realtime;

public interface IOrdersClient
{
    Task OrderCreated(OrderRealtimeDto order);

    Task OrderStatusChanged(OrderRealtimeDto order);

    Task OrderCancelled(OrderRealtimeDto order);
}
