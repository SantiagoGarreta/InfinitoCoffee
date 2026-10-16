namespace InfinitoCoffee.Application.Orders.Queries;

public sealed record GetOrderHistoryQuery(int Take = 20, string? Cursor = null);
