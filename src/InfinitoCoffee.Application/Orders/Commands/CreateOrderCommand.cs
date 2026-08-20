namespace InfinitoCoffee.Application.Orders.Commands;

public sealed record CreateOrderCommand(
    string? Notes,
    IReadOnlyCollection<CreateOrderItemCommand> Items);
