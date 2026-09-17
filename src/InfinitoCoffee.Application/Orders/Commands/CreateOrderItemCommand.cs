namespace InfinitoCoffee.Application.Orders.Commands;

public sealed record CreateOrderItemCommand(
    Guid ProductId,
    int Quantity,
    string? Notes);
