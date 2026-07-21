namespace InfinitoCoffee.Domain.Orders.Exceptions;

public sealed class InvalidOrderStateTransitionException : DomainException
{
    public InvalidOrderStateTransitionException(OrderStatus currentStatus, OrderStatus targetStatus)
        : base($"The transition from '{currentStatus}' to '{targetStatus}' is not allowed.")
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    public OrderStatus CurrentStatus { get; }

    public OrderStatus TargetStatus { get; }
}
