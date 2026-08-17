namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed record PickupOrderResponse(
    Guid Id,
    string OrderNumber,
    string Status,
    DateTime CreatedAtUtc);
