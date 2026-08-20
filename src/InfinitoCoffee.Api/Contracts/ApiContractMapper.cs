using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Api.Contracts.ProductCategories;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Application.Orders.Dtos;
using InfinitoCoffee.Application.ProductCategories.Dtos;
using InfinitoCoffee.Application.Products.Dtos;
using InfinitoCoffee.Api.Contracts.Users;
using InfinitoCoffee.Application.Users.Dtos;

namespace InfinitoCoffee.Api.Contracts;

internal static class ApiContractMapper
{
    public static OrderResponse MapOrder(OrderDto order)
    {
        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.CreatedAtUtc,
            order.StartedAtUtc,
            order.ReadyAtUtc,
            order.DeliveredAtUtc,
            order.CancelledAtUtc,
            order.Notes,
            order.Total,
            order.Items.Select(MapOrderItem).ToArray());
    }

    public static PickupOrderResponse MapPickupOrder(OrderDto order)
    {
        return new PickupOrderResponse(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.CreatedAtUtc);
    }

    public static PickupOrderResponse MapPickupOrder(OrderRealtimeDto order)
    {
        return new PickupOrderResponse(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.CreatedAtUtc);
    }

    public static OrderResultsResponse MapOrderResults(OrderResultsDto results)
    {
        return new OrderResultsResponse(
            results.GroupBy.ToString(),
            results.PeriodStartDate,
            results.PeriodEndDate,
            MapOrderPeriodSummary(results.CurrentPeriod),
            MapOrderPeriodSummary(results.PreviousPeriod),
            MapOperationalSnapshot(results.OperationalSnapshot),
            results.TopSellingProducts.Select(MapTopSellingProduct).ToArray(),
            results.History.Select(MapHistoryPoint).ToArray());
    }

    public static ProductResponse MapProduct(ProductDto product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Cost,
            product.CategoryId,
            product.IsActive);
    }

    public static ProductCategoryResponse MapProductCategory(ProductCategoryDto category)
    {
        return new ProductCategoryResponse(
            category.Id,
            category.Name,
            category.IsActive);
    }

    public static UserResponse MapUser(UserDto user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.IsActive,
            user.IsSystemUser);
    }

    public static OrderResultsGroupBy ParseOrderResultsGroupBy(string? groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return OrderResultsGroupBy.Daily;
        }

        if (!Enum.TryParse<OrderResultsGroupBy>(groupBy, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new ArgumentException(
                $"Order results groupBy '{groupBy}' is invalid. Allowed values: {string.Join(", ", Enum.GetNames<OrderResultsGroupBy>())}.",
                nameof(groupBy));
        }

        return parsed;
    }

    private static OrderItemResponse MapOrderItem(OrderItemDto item)
    {
        return new OrderItemResponse(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.UnitPrice,
            item.Quantity,
            item.Notes,
            item.LineTotal);
    }

    private static OrderPeriodSummaryResponse MapOrderPeriodSummary(OrderPeriodSummaryDto summary)
    {
        return new OrderPeriodSummaryResponse(
            summary.StartDate,
            summary.EndDate,
            summary.TotalRevenue,
            summary.TotalCost,
            summary.TotalProfit,
            summary.TotalOrdersCount,
            summary.DeliveredOrdersCount,
            summary.CancelledOrdersCount,
            summary.DeliveredItemsCount,
            summary.AverageDeliveredOrderTotal);
    }

    private static OrderOperationalSnapshotResponse MapOperationalSnapshot(OrderOperationalSnapshotDto snapshot)
    {
        return new OrderOperationalSnapshotResponse(
            snapshot.TotalOrdersCount,
            snapshot.ActiveOrdersCount,
            snapshot.PendingOrdersCount,
            snapshot.PreparingOrdersCount,
            snapshot.ReadyOrdersCount,
            snapshot.DeliveredOrdersCount,
            snapshot.CancelledOrdersCount);
    }

    private static OrderHistoryPointResponse MapHistoryPoint(OrderHistoryPointDto historyPoint)
    {
        return new OrderHistoryPointResponse(
            historyPoint.StartDate,
            historyPoint.EndDate,
            historyPoint.TotalRevenue,
            historyPoint.TotalCost,
            historyPoint.TotalProfit,
            historyPoint.TotalOrdersCount,
            historyPoint.DeliveredOrdersCount,
            historyPoint.CancelledOrdersCount,
            historyPoint.DeliveredItemsCount,
            historyPoint.AverageDeliveredOrderTotal);
    }

    private static TopSellingProductResponse MapTopSellingProduct(TopSellingProductDto product)
    {
        return new TopSellingProductResponse(
            product.ProductId,
            product.ProductName,
            product.QuantitySold,
            product.Revenue);
    }
}
