using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Common.Time;
using InfinitoCoffee.Application.Orders.Commands;
using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Dtos;
using InfinitoCoffee.Application.Orders.Queries;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Services;

public sealed class OrderService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OrderDto> CreateOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var orderNumber = NormalizeRequired(command.OrderNumber, nameof(command.OrderNumber));
        var items = command.Items?.ToList() ?? throw new ArgumentNullException(nameof(command.Items));

        if (items.Count == 0)
        {
            throw new ArgumentException("At least one item is required.", nameof(command.Items));
        }

        if (await _orderRepository.OrderNumberExistsAsync(orderNumber, cancellationToken))
        {
            throw new ConflictException($"The order number '{orderNumber}' already exists.");
        }

        var orderItems = new List<OrderItem>(items.Count);

        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.Quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(item.Quantity), "Quantity must be greater than zero.");
            }

            var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new NotFoundException("Product", item.ProductId);

            if (!product.IsActive)
            {
                throw new ConflictException($"The product '{product.Name}' is inactive.");
            }

            orderItems.Add(new OrderItem(
                product.Id,
                product.Name,
                product.Price,
                item.Quantity,
                item.Notes));
        }

        var order = new Order(
            orderNumber,
            command.Source,
            _dateTimeProvider.UtcNow,
            orderItems,
            command.Notes);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return MapOrder(order);
    }

    public async Task<OrderDto> GetOrderByIdAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await _orderRepository.GetByIdAsync(query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", query.OrderId);

        return MapOrder(order);
    }

    public async Task<IReadOnlyCollection<OrderDto>> GetActiveOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetActiveAsync(cancellationToken);

        return orders
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber, StringComparer.Ordinal)
            .Select(MapOrder)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<OrderDto>> GetPickupOrdersAsync(
        GetPickupOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ReadyVisibilityDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.ReadyVisibilityDuration),
                "Ready visibility duration cannot be negative.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var orders = await _orderRepository.GetPickupCandidatesAsync(cancellationToken);

        return orders
            .Where(order => order.Status == OrderStatus.Preparing
                || order.IsVisibleForPickup(utcNow, query.ReadyVisibilityDuration))
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber, StringComparer.Ordinal)
            .Select(MapOrder)
            .ToArray();
    }

    public Task<OrderDto> StartOrderPreparationAsync(
        StartOrderPreparationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyStatusTransitionAsync(
            command.OrderId,
            order => order.StartPreparing(_dateTimeProvider.UtcNow),
            cancellationToken);
    }

    public Task<OrderDto> MarkOrderAsReadyAsync(
        MarkOrderAsReadyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyStatusTransitionAsync(
            command.OrderId,
            order => order.MarkReady(_dateTimeProvider.UtcNow),
            cancellationToken);
    }

    public Task<OrderDto> DeliverOrderAsync(
        DeliverOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyStatusTransitionAsync(
            command.OrderId,
            order => order.Deliver(_dateTimeProvider.UtcNow),
            cancellationToken);
    }

    public Task<OrderDto> CancelOrderAsync(
        CancelOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyStatusTransitionAsync(
            command.OrderId,
            order => order.Cancel(_dateTimeProvider.UtcNow),
            cancellationToken);
    }

    private async Task<OrderDto> ApplyStatusTransitionAsync(
        Guid orderId,
        Action<Order> transition,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        transition(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return MapOrder(order);
    }

    private static OrderDto MapOrder(Order order)
    {
        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.Source,
            order.Status,
            order.CreatedAtUtc,
            order.StartedAtUtc,
            order.ReadyAtUtc,
            order.DeliveredAtUtc,
            order.CancelledAtUtc,
            order.Notes,
            order.Total,
            order.Items.Select(MapOrderItem).ToArray());
    }

    private static OrderItemDto MapOrderItem(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductId,
            item.ProductNameSnapshot,
            item.UnitPriceSnapshot,
            item.Quantity,
            item.Notes,
            item.LineTotal);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value is required.", parameterName);
        }

        return value.Trim();
    }
}
