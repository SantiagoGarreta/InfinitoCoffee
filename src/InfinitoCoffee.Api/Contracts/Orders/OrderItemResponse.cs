namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductNameSnapshot,
    decimal UnitPriceSnapshot,
    int Quantity,
    string? Notes,
    decimal LineTotal);
