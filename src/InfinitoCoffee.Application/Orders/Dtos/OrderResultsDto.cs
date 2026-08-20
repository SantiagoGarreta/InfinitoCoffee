namespace InfinitoCoffee.Application.Orders.Dtos;

public enum OrderResultsGroupBy
{
    Daily,
    Weekly,
    Monthly,
}

public sealed record OrderResultsDto(
    OrderResultsGroupBy GroupBy,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    OrderPeriodSummaryDto CurrentPeriod,
    OrderPeriodSummaryDto PreviousPeriod,
    OrderOperationalSnapshotDto OperationalSnapshot,
    IReadOnlyCollection<TopSellingProductDto> TopSellingProducts,
    IReadOnlyCollection<OrderHistoryPointDto> History);

public sealed record OrderPeriodSummaryDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalRevenue,
    decimal TotalCost,
    decimal TotalProfit,
    int TotalOrdersCount,
    int DeliveredOrdersCount,
    int CancelledOrdersCount,
    int DeliveredItemsCount,
    decimal AverageDeliveredOrderTotal);

public sealed record OrderOperationalSnapshotDto(
    int TotalOrdersCount,
    int ActiveOrdersCount,
    int PendingOrdersCount,
    int PreparingOrdersCount,
    int ReadyOrdersCount,
    int DeliveredOrdersCount,
    int CancelledOrdersCount);

public sealed record OrderHistoryPointDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalRevenue,
    decimal TotalCost,
    decimal TotalProfit,
    int TotalOrdersCount,
    int DeliveredOrdersCount,
    int CancelledOrdersCount,
    int DeliveredItemsCount,
    decimal AverageDeliveredOrderTotal);

public sealed record TopSellingProductDto(
    Guid ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue);
