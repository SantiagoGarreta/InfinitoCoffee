using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Orders.Commands;
using InfinitoCoffee.Application.Orders.Queries;
using InfinitoCoffee.Application.Orders.Services;
using InfinitoCoffee.Application.Tests.Fakes;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Orders.Exceptions;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.Orders;

public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_ValidCommand_CreatesOrder()
    {
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc)
        };
        var orderRepository = new FakeOrderRepository();
        var productRepository = new FakeProductRepository();
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var product = new Product(Guid.NewGuid(), "Flat White", 6.50m, "Double shot");
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository, dateTimeProvider, eventPublisher);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            "Sin azucar",
            [new CreateOrderItemCommand(product.Id, 2, "Extra hot")]));

        Assert.Equal("260721-0001", result.OrderNumber);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(dateTimeProvider.UtcNow, result.CreatedAtUtc);
        Assert.Equal(13m, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(1, orderRepository.SaveChangesCalls);
        var publishedEvent = Assert.Single(eventPublisher.Events);
        Assert.Equal("OrderCreated", publishedEvent.EventName);
        Assert.Equal(result.Id, publishedEvent.Order.Id);
        Assert.Equal(1, publishedEvent.SaveChangesCallsAtPublish);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidCommand_UsesProductSnapshot()
    {
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc)
        };
        var orderRepository = new FakeOrderRepository();
        var productRepository = new FakeProductRepository();
        var categoryId = Guid.NewGuid();
        var product = new Product(categoryId, "Latte", 7.25m, "Oat milk");
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository, dateTimeProvider);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        product.Rename("Changed");
        product.ChangePrice(99m);

        var item = Assert.Single(result.Items);
        Assert.Equal("Latte", item.ProductName);
        Assert.Equal(7.25m, item.UnitPrice);
    }

    [Fact]
    public async Task CreateOrderAsync_DuplicateOrderNumber_ThrowsConflictException()
    {
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(CreatePendingOrder("260721-0001"));
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Espresso", 4m);
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository);

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        await Assert.ThrowsAsync<ConflictException>(action);
        Assert.Equal(0, orderRepository.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateOrderAsync_EmptyItems_ThrowsArgumentException()
    {
        var service = CreateOrderService(new FakeOrderRepository(), new FakeProductRepository());

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            []));

        var exception = await Assert.ThrowsAsync<ArgumentException>(action);
        Assert.Equal("Items", exception.ParamName);
    }

    [Fact]
    public async Task CreateOrderAsync_MissingProduct_ThrowsNotFoundException()
    {
        var service = CreateOrderService(new FakeOrderRepository(), new FakeProductRepository());

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            [new CreateOrderItemCommand(Guid.NewGuid(), 1, null)]));

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task CreateOrderAsync_InactiveProduct_ThrowsConflictException()
    {
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Mocha", 8m);
        product.Deactivate();
        productRepository.Seed(product);
        var service = CreateOrderService(new FakeOrderRepository(), productRepository);

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task CreateOrderAsync_WithCancellationToken_PropagatesCancellationToken()
    {
        var tokenSource = new CancellationTokenSource();
        var orderRepository = new FakeOrderRepository();
        var productRepository = new FakeProductRepository();
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var product = new Product(Guid.NewGuid(), "Americano", 5m);
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository, eventPublisher: eventPublisher);

        await service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]), tokenSource.Token);

        Assert.Equal(tokenSource.Token, orderRepository.LastOrderNumberExistsToken);
        Assert.Equal(tokenSource.Token, productRepository.LastGetByIdToken);
        Assert.Equal(tokenSource.Token, orderRepository.LastAddToken);
        Assert.Equal(tokenSource.Token, orderRepository.LastSaveChangesToken);
        Assert.Equal(tokenSource.Token, Assert.Single(eventPublisher.Events).CancellationToken);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ExistingOrder_ReturnsOrder()
    {
        var order = CreatePendingOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var service = CreateOrderService(orderRepository, new FakeProductRepository());

        var result = await service.GetOrderByIdAsync(new GetOrderByIdQuery(order.Id));

        Assert.Equal(order.Id, result.Id);
    }

    [Fact]
    public async Task GetOrderByIdAsync_CancelledOrder_ReturnsCancelledAtUtc()
    {
        var cancelledAtUtc = new DateTime(2026, 7, 21, 12, 9, 0, DateTimeKind.Utc);
        var order = CreateCancelledOrder("260721-0001", cancelledAtUtc: cancelledAtUtc);
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var service = CreateOrderService(orderRepository, new FakeProductRepository());

        var result = await service.GetOrderByIdAsync(new GetOrderByIdQuery(order.Id));

        Assert.Equal(cancelledAtUtc, result.CancelledAtUtc);
    }

    [Fact]
    public async Task GetOrderByIdAsync_MissingOrder_ThrowsNotFoundException()
    {
        var service = CreateOrderService(new FakeOrderRepository(), new FakeProductRepository());

        var action = () => service.GetOrderByIdAsync(new GetOrderByIdQuery(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task GetActiveOrdersAsync_WithMixedStatuses_ReturnsActiveOrdersOrderedByCreatedAtUtc()
    {
        var pending = CreatePendingOrder("260721-0003", createdAtUtc: new DateTime(2026, 7, 21, 12, 3, 0, DateTimeKind.Utc));
        var preparing = CreatePreparingOrder("260721-0002", createdAtUtc: new DateTime(2026, 7, 21, 12, 2, 0, DateTimeKind.Utc));
        var delivered = CreateDeliveredOrder("260721-0001", createdAtUtc: new DateTime(2026, 7, 21, 12, 1, 0, DateTimeKind.Utc));
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(pending, preparing, delivered);
        var service = CreateOrderService(orderRepository, new FakeProductRepository());

        var result = await service.GetActiveOrdersAsync();

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            first => Assert.Equal("260721-0002", first.OrderNumber),
            second => Assert.Equal("260721-0003", second.OrderNumber));
    }

    [Fact]
    public async Task GetPickupOrdersAsync_MixedStatuses_FiltersPickupScreenCorrectly()
    {
        var preparing = CreatePreparingOrder("260721-0001");
        var visibleReady = CreateReadyOrder(
            "260721-0002",
            readyAtUtc: new DateTime(2026, 7, 21, 12, 10, 0, DateTimeKind.Utc));
        var hiddenReady = CreateReadyOrder(
            "260721-0003",
            readyAtUtc: new DateTime(2026, 7, 21, 11, 30, 0, DateTimeKind.Utc));
        var cancelled = CreateCancelledOrder("260721-0004");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(preparing, visibleReady, hiddenReady, cancelled);
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 20, 0, DateTimeKind.Utc)
        };
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider);

        var result = await service.GetPickupOrdersAsync(new GetPickupOrdersQuery(TimeSpan.FromMinutes(15)));

        Assert.Collection(
            result,
            first => Assert.Equal("260721-0001", first.OrderNumber),
            second => Assert.Equal("260721-0002", second.OrderNumber));
    }

    [Fact]
    public async Task GetPickupOrdersAsync_ReadyOutsideVisibilityWindow_HidesOrder()
    {
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(CreateReadyOrder(
            "260721-0001",
            readyAtUtc: new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc)));
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 16, 0, DateTimeKind.Utc)
        };
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider);

        var result = await service.GetPickupOrdersAsync(new GetPickupOrdersQuery(TimeSpan.FromMinutes(15)));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOrderByIdAsync_NonCancelledOrder_ReturnsNullCancelledAtUtc()
    {
        var order = CreatePendingOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var service = CreateOrderService(orderRepository, new FakeProductRepository());

        var result = await service.GetOrderByIdAsync(new GetOrderByIdQuery(order.Id));

        Assert.Null(result.CancelledAtUtc);
    }

    [Fact]
    public async Task StartOrderPreparationAsync_ExistingPendingOrder_StartsPreparation()
    {
        var order = CreatePendingOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 5, 0, DateTimeKind.Utc)
        };
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider, eventPublisher);

        var result = await service.StartOrderPreparationAsync(new StartOrderPreparationCommand(order.Id));

        Assert.Equal(OrderStatus.Preparing, result.Status);
        Assert.Equal(dateTimeProvider.UtcNow, result.StartedAtUtc);
        Assert.Equal(1, orderRepository.SaveChangesCalls);
        var publishedEvent = Assert.Single(eventPublisher.Events);
        Assert.Equal("OrderStatusChanged", publishedEvent.EventName);
        Assert.Equal("Preparing", publishedEvent.Order.Status);
        Assert.Equal(1, publishedEvent.SaveChangesCallsAtPublish);
    }

    [Fact]
    public async Task MarkOrderAsReadyAsync_PreparingOrder_MarksAsReady()
    {
        var order = CreatePreparingOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 9, 0, DateTimeKind.Utc)
        };
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider, eventPublisher);

        var result = await service.MarkOrderAsReadyAsync(new MarkOrderAsReadyCommand(order.Id));

        Assert.Equal(OrderStatus.Ready, result.Status);
        Assert.Equal(dateTimeProvider.UtcNow, result.ReadyAtUtc);
        var publishedEvent = Assert.Single(eventPublisher.Events);
        Assert.Equal("OrderStatusChanged", publishedEvent.EventName);
        Assert.Equal("Ready", publishedEvent.Order.Status);
    }

    [Fact]
    public async Task DeliverOrderAsync_ReadyOrder_DeliversOrder()
    {
        var order = CreateReadyOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 12, 0, DateTimeKind.Utc)
        };
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider, eventPublisher);

        var result = await service.DeliverOrderAsync(new DeliverOrderCommand(order.Id));

        Assert.Equal(OrderStatus.Delivered, result.Status);
        Assert.Equal(dateTimeProvider.UtcNow, result.DeliveredAtUtc);
        var publishedEvent = Assert.Single(eventPublisher.Events);
        Assert.Equal("OrderStatusChanged", publishedEvent.EventName);
        Assert.Equal("Delivered", publishedEvent.Order.Status);
    }

    [Fact]
    public async Task CancelOrderAsync_ExistingOrder_CancelsOrder()
    {
        var order = CreatePreparingOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 7, 21, 12, 13, 0, DateTimeKind.Utc)
        };
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider, eventPublisher);

        var result = await service.CancelOrderAsync(new CancelOrderCommand(order.Id));

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.Equal(dateTimeProvider.UtcNow, result.CancelledAtUtc);
        var publishedEvent = Assert.Single(eventPublisher.Events);
        Assert.Equal("OrderCancelled", publishedEvent.EventName);
        Assert.Equal(result.CancelledAtUtc, publishedEvent.Order.CancelledAtUtc);
    }

    [Fact]
    public async Task StartOrderPreparationAsync_MissingOrder_ThrowsNotFoundException()
    {
        var orderRepository = new FakeOrderRepository();
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), eventPublisher: eventPublisher);

        var action = () => service.StartOrderPreparationAsync(new StartOrderPreparationCommand(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal(0, orderRepository.SaveChangesCalls);
        Assert.Empty(eventPublisher.Events);
    }

    [Fact]
    public async Task MarkOrderAsReadyAsync_InvalidTransition_PropagatesDomainException()
    {
        var order = CreatePendingOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), eventPublisher: eventPublisher);

        var action = () => service.MarkOrderAsReadyAsync(new MarkOrderAsReadyCommand(order.Id));

        await Assert.ThrowsAsync<InvalidOrderStateTransitionException>(action);
        Assert.Equal(0, orderRepository.SaveChangesCalls);
        Assert.Empty(eventPublisher.Events);
    }

    [Fact]
    public async Task DeliverOrderAsync_Success_SavesChangesOnce()
    {
        var order = CreateReadyOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var service = CreateOrderService(orderRepository, new FakeProductRepository());

        await service.DeliverOrderAsync(new DeliverOrderCommand(order.Id));

        Assert.Equal(1, orderRepository.SaveChangesCalls);
    }

    [Fact]
    public async Task CancelOrderAsync_InvalidTransition_DoesNotSaveChanges()
    {
        var order = CreateDeliveredOrder("260721-0001");
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(order);
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), eventPublisher: eventPublisher);

        var action = () => service.CancelOrderAsync(new CancelOrderCommand(order.Id));

        await Assert.ThrowsAsync<InvalidOrderStateTransitionException>(action);
        Assert.Equal(0, orderRepository.SaveChangesCalls);
        Assert.Empty(eventPublisher.Events);
    }

    [Fact]
    public async Task CreateOrderAsync_SaveChangesFails_DoesNotPublishEvent()
    {
        var orderRepository = new FakeOrderRepository
        {
            SaveChangesException = new InvalidOperationException("Persistence failed.")
        };
        var productRepository = new FakeProductRepository();
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var product = new Product(Guid.NewGuid(), "Americano", 5m);
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository, eventPublisher: eventPublisher);

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            "260721-0001",
            OrderSource.Counter,
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Empty(eventPublisher.Events);
        Assert.Equal(0, orderRepository.SaveChangesCalls);
    }

    private static OrderService CreateOrderService(
        FakeOrderRepository orderRepository,
        FakeProductRepository productRepository,
        FakeDateTimeProvider? dateTimeProvider = null,
        FakeOrderEventPublisher? eventPublisher = null)
    {
        return new OrderService(
            orderRepository,
            productRepository,
            dateTimeProvider ?? new FakeDateTimeProvider
            {
                UtcNow = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc)
            },
            eventPublisher ?? new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls));
    }

    private static Order CreatePendingOrder(string orderNumber, DateTime? createdAtUtc = null)
    {
        return new Order(
            orderNumber,
            OrderSource.Counter,
            createdAtUtc ?? new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            [new OrderItem(Guid.NewGuid(), "Latte", 7.25m, 1)]);
    }

    private static Order CreatePreparingOrder(string orderNumber, DateTime? createdAtUtc = null)
    {
        var order = CreatePendingOrder(orderNumber, createdAtUtc);
        order.StartPreparing(order.CreatedAtUtc.AddMinutes(2));
        return order;
    }

    private static Order CreateReadyOrder(string orderNumber, DateTime? createdAtUtc = null, DateTime? readyAtUtc = null)
    {
        var order = CreatePreparingOrder(orderNumber, createdAtUtc);
        order.MarkReady(readyAtUtc ?? order.CreatedAtUtc.AddMinutes(5));
        return order;
    }

    private static Order CreateDeliveredOrder(string orderNumber, DateTime? createdAtUtc = null)
    {
        var order = CreateReadyOrder(orderNumber, createdAtUtc);
        order.Deliver(order.ReadyAtUtc!.Value.AddMinutes(2));
        return order;
    }

    private static Order CreateCancelledOrder(string orderNumber, DateTime? createdAtUtc = null, DateTime? cancelledAtUtc = null)
    {
        var order = CreatePendingOrder(orderNumber, createdAtUtc);
        order.Cancel(cancelledAtUtc ?? order.CreatedAtUtc.AddMinutes(4));
        return order;
    }
}
