namespace InfinitoCoffee.Application.Orders.Dtos;

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? Notes,
    decimal LineTotal);
