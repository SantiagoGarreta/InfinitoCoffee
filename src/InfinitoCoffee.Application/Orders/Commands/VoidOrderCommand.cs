namespace InfinitoCoffee.Application.Orders.Commands;

public sealed record VoidOrderCommand(Guid OrderId, Guid ActorId);
