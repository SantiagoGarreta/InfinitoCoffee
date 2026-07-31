using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Commands;

public sealed record CreateOrderCommand(
    OrderSource Source,
    string? Notes,
    IReadOnlyCollection<CreateOrderItemCommand> Items);
