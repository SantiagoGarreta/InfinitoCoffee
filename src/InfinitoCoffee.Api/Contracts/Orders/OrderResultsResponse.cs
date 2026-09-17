namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed record OrderResultsResponse(
    string GroupBy,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    OrderPeriodSummaryResponse CurrentPeriod,
    OrderPeriodSummaryResponse PreviousPeriod,
    OrderOperationalSnapshotResponse OperationalSnapshot,
    IReadOnlyCollection<TopSellingProductResponse> TopSellingProducts,
    IReadOnlyCollection<OrderHistoryPointResponse> History);

public sealed record OrderPeriodSummaryResponse(
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

public sealed record OrderOperationalSnapshotResponse(
    int TotalOrdersCount,
    int ActiveOrdersCount,
    int PendingOrdersCount,
    int PreparingOrdersCount,
    int ReadyOrdersCount,
    int DeliveredOrdersCount,
    int CancelledOrdersCount);

public sealed record OrderHistoryPointResponse(
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

public sealed record TopSellingProductResponse(
    Guid ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue);
