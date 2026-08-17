using System.Globalization;
using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Common.Time;
using InfinitoCoffee.Application.Orders.Commands;
using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Dtos;
using InfinitoCoffee.Application.Orders.Queries;
using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Services;

public sealed class OrderService
{
    private const int FirstDisplayOrderNumber = 1;
    private const int LastDisplayOrderNumber = 99;

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOrderEventPublisher _orderEventPublisher;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly IProductRepository _productRepository;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        IDateTimeProvider dateTimeProvider,
        IOrderEventPublisher orderEventPublisher)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _dateTimeProvider = dateTimeProvider;
        _orderEventPublisher = orderEventPublisher;
    }

    public async Task<OrderDto> CreateOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var items = command.Items?.ToList() ?? throw new ArgumentNullException(nameof(command.Items));

        if (items.Count == 0)
        {
            throw new ArgumentException("At least one item is required.", nameof(command.Items));
        }

        var validatedItems = new List<(CreateOrderItemCommand Item, Domain.Products.Product Product)>(items.Count);

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

            var category = await _productCategoryRepository.GetByIdAsync(product.CategoryId, cancellationToken)
                ?? throw new NotFoundException("ProductCategory", product.CategoryId);

            if (!category.IsActive)
            {
                throw new ConflictException($"The category '{category.Name}' is inactive.");
            }

            validatedItems.Add((item, product));
        }

        var orderItems = validatedItems
            .Select(validated => new OrderItem(
                validated.Product.Id,
                validated.Product.Name,
                validated.Product.Price,
                validated.Item.Quantity,
                validated.Item.Notes))
            .ToArray();

        var orderNumber = await GenerateNextOrderNumberAsync(cancellationToken);
        var order = new Order(
            orderNumber,
            command.Source,
            _dateTimeProvider.UtcNow,
            orderItems,
            command.Notes);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        var orderDto = MapOrder(order);
        await _orderEventPublisher.OrderCreatedAsync(MapRealtimeOrder(orderDto), cancellationToken);

        return orderDto;
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
            .ThenBy(order => order.OrderNumber, Comparer<string>.Create(CompareOrderNumbers))
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
            .ThenBy(order => order.OrderNumber, Comparer<string>.Create(CompareOrderNumbers))
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
            order => _orderEventPublisher.OrderStatusChangedAsync(order, cancellationToken),
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
            order => _orderEventPublisher.OrderStatusChangedAsync(order, cancellationToken),
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
            order => _orderEventPublisher.OrderStatusChangedAsync(order, cancellationToken),
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
            order => _orderEventPublisher.OrderCancelledAsync(order, cancellationToken),
            cancellationToken);
    }

    private async Task<OrderDto> ApplyStatusTransitionAsync(
        Guid orderId,
        Action<Order> transition,
        Func<OrderRealtimeDto, Task> publishEvent,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        transition(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        var orderDto = MapOrder(order);
        await publishEvent(MapRealtimeOrder(orderDto));

        return orderDto;
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

    private static OrderRealtimeDto MapRealtimeOrder(OrderDto order)
    {
        return new OrderRealtimeDto(
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
            order.Items.Select(MapRealtimeOrderItem).ToArray());
    }

    private static OrderRealtimeItemDto MapRealtimeOrderItem(OrderItemDto item)
    {
        return new OrderRealtimeItemDto(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.UnitPrice,
            item.Quantity,
            item.Notes,
            item.LineTotal);
    }

    private async Task<string> GenerateNextOrderNumberAsync(CancellationToken cancellationToken)
    {
        var latestOrderNumber = await _orderRepository.GetLatestOrderNumberAsync(cancellationToken);

        if (!TryParseDisplayOrderNumber(latestOrderNumber, out var latestDisplayOrderNumber))
        {
            return FirstDisplayOrderNumber.ToString(CultureInfo.InvariantCulture);
        }

        var nextDisplayOrderNumber = latestDisplayOrderNumber >= LastDisplayOrderNumber
            ? FirstDisplayOrderNumber
            : latestDisplayOrderNumber + 1;

        return nextDisplayOrderNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static int CompareOrderNumbers(string? left, string? right)
    {
        var leftIsDisplayNumber = TryParseDisplayOrderNumber(left, out var leftDisplayNumber);
        var rightIsDisplayNumber = TryParseDisplayOrderNumber(right, out var rightDisplayNumber);

        if (leftIsDisplayNumber && rightIsDisplayNumber)
        {
            return leftDisplayNumber.CompareTo(rightDisplayNumber);
        }

        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private static bool TryParseDisplayOrderNumber(string? value, out int displayOrderNumber)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out displayOrderNumber))
        {
            displayOrderNumber = default;
            return false;
        }

        return displayOrderNumber is >= FirstDisplayOrderNumber and <= LastDisplayOrderNumber;
    }
}
