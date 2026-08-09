using InfinitoCoffee.Api.Contracts.Orders;

namespace InfinitoCoffee.Api.Realtime;

public interface IPickupClient
{
    Task OrderStatusChanged(PickupOrderResponse order);

    Task OrderCancelled(PickupOrderResponse order);
}
