namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed record PickupOrderItemResponse(
    Guid Id,
    string ProductName,
    int Quantity);
