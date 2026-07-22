namespace InfinitoCoffee.Application.Orders.Dtos;

public sealed record OrderRealtimeDto(
    Guid Id,
    string OrderNumber,
    string Source,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc,
    decimal Total);
