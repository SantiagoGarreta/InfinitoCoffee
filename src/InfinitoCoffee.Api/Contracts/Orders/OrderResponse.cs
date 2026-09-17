namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc,
    string? Notes,
    decimal Total,
    IReadOnlyCollection<OrderItemResponse> Items);
