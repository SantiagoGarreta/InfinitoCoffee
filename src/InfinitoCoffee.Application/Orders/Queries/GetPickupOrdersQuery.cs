namespace InfinitoCoffee.Application.Orders.Queries;

public sealed record GetPickupOrdersQuery(TimeSpan ReadyVisibilityDuration);
