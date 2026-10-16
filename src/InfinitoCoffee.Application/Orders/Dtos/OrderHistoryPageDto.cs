namespace InfinitoCoffee.Application.Orders.Dtos;

public sealed record OrderHistoryPageDto(
    IReadOnlyCollection<OrderDto> Items,
    string? NextCursor);
