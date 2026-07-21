using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Commands;

public sealed record CreateOrderCommand(
    string OrderNumber,
    OrderSource Source,
    string? Notes,
    IReadOnlyCollection<CreateOrderItemCommand> Items);
