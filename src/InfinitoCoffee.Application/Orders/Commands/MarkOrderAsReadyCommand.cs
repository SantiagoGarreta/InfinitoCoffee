namespace InfinitoCoffee.Application.Orders.Commands;

public sealed record MarkOrderAsReadyCommand(Guid OrderId);
