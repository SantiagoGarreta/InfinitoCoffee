using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Dtos;

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc,
    string? Notes,
    decimal Total,
    IReadOnlyCollection<OrderItemDto> Items);
