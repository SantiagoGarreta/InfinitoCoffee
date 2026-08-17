using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Api.Contracts.ProductCategories;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Application.Orders.Dtos;
using InfinitoCoffee.Application.ProductCategories.Dtos;
using InfinitoCoffee.Application.Products.Dtos;
using InfinitoCoffee.Api.Contracts.Users;
using InfinitoCoffee.Application.Users.Dtos;
using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Api.Contracts;

internal static class ApiContractMapper
{
    public static OrderResponse MapOrder(OrderDto order)
    {
        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            order.Source.ToString(),
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

    public static ProductResponse MapProduct(ProductDto product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
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

    public static OrderSource ParseOrderSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Order source is required.", nameof(source));
        }

        if (!Enum.TryParse<OrderSource>(source, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new ArgumentException(
                $"Order source '{source}' is invalid. Allowed values: {string.Join(", ", Enum.GetNames<OrderSource>())}.",
                nameof(source));
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
}
