namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed record OrderHistoryPageResponse(
    IReadOnlyCollection<OrderResponse> Items,
    string? NextCursor);
