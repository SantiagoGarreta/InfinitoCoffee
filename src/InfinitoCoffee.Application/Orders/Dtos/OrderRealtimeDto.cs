namespace InfinitoCoffee.Application.Orders.Dtos;

public sealed record OrderRealtimeDto(
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
    IReadOnlyCollection<OrderRealtimeItemDto> Items);
