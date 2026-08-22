using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Orders.Commands;
using InfinitoCoffee.Application.Orders.Dtos;
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
        var product = new Product(Guid.NewGuid(), "Flat White", 6.50m, 2.75m, "Double shot");
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository, dateTimeProvider, eventPublisher);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            "Sin azucar",
            [new CreateOrderItemCommand(product.Id, 2, "Extra hot")]));

        Assert.Equal("1", result.OrderNumber);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal(dateTimeProvider.UtcNow, result.CreatedAtUtc);
        Assert.Equal(13m, result.Total);
        Assert.Single(result.Items);
        var storedOrder = Assert.Single(await orderRepository.GetAllAsync());
        Assert.Equal(5.50m, storedOrder.TotalCost);
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
        var product = new Product(categoryId, "Latte", 7.25m, 2.80m, "Oat milk");
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository, dateTimeProvider);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        product.Rename("Changed");
        product.ChangePrice(99m);
        product.ChangeCost(0.50m);

        var item = Assert.Single(result.Items);
        Assert.Equal("Latte", item.ProductName);
        Assert.Equal(7.25m, item.UnitPrice);
        var storedOrder = Assert.Single(await orderRepository.GetAllAsync());
        Assert.Equal(4.45m, storedOrder.Profit);
    }

    [Fact]
    public async Task CreateOrderAsync_WithExistingDisplayOrderNumber_IncrementsOrderNumber()
    {
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(CreatePendingOrder("7"));
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Espresso", 4m);
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        Assert.Equal("8", result.OrderNumber);
        Assert.Equal(1, orderRepository.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenLatestDisplayOrderNumberIs99_WrapsTo1()
    {
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(CreatePendingOrder("99"));
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Espresso", 4m);
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        Assert.Equal("1", result.OrderNumber);
    }

    [Fact]
    public async Task CreateOrderAsync_WithLegacyLatestOrderNumber_StartsFrom1()
    {
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(CreatePendingOrder("A-101"));
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Espresso", 4m);
        productRepository.Seed(product);
        var service = CreateOrderService(orderRepository, productRepository);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        Assert.Equal("1", result.OrderNumber);
    }

    [Fact]
    public async Task CreateOrderAsync_EmptyItems_ThrowsArgumentException()
    {
        var service = CreateOrderService(new FakeOrderRepository(), new FakeProductRepository());

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
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
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task CreateOrderAsync_ActiveProductWithInactiveCategory_ThrowsConflictException()
    {
        var category = new ProductCategory("Coffee");
        category.Deactivate();
        var categoryRepository = new FakeProductCategoryRepository();
        categoryRepository.Seed(category);
        var productRepository = new FakeProductRepository();
        var product = new Product(category.Id, "Mocha", 8m);
        productRepository.Seed(product);
        var service = CreateOrderService(
            new FakeOrderRepository(),
            productRepository,
            productCategoryRepository: categoryRepository);

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCategoryIsReactivated_AllowsOrderAgain()
    {
        var category = new ProductCategory("Coffee");
        category.Deactivate();
        category.Activate();
        var categoryRepository = new FakeProductCategoryRepository();
        categoryRepository.Seed(category);
        var productRepository = new FakeProductRepository();
        var product = new Product(category.Id, "Mocha", 8m);
        productRepository.Seed(product);
        var service = CreateOrderService(
            new FakeOrderRepository(),
            productRepository,
            productCategoryRepository: categoryRepository);

        var result = await service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task CreateOrderAsync_MissingProductCategory_ThrowsNotFoundException()
    {
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Mocha", 8m);
        productRepository.Seed(product);
        var service = CreateOrderService(
            new FakeOrderRepository(),
            productRepository,
            productCategoryRepository: new FakeProductCategoryRepository());

        var action = () => service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]));

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task CreateOrderAsync_WithCancellationToken_PropagatesCancellationToken()
    {
        var tokenSource = new CancellationTokenSource();
        var orderRepository = new FakeOrderRepository();
        var productRepository = new FakeProductRepository();
        var categoryRepository = new FakeProductCategoryRepository();
        var eventPublisher = new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls);
        var category = new ProductCategory("Coffee");
        categoryRepository.Seed(category);
        var product = new Product(category.Id, "Americano", 5m);
        productRepository.Seed(product);
        var service = CreateOrderService(
            orderRepository,
            productRepository,
            eventPublisher: eventPublisher,
            productCategoryRepository: categoryRepository);

        await service.CreateOrderAsync(new CreateOrderCommand(
            null,
            [new CreateOrderItemCommand(product.Id, 1, null)]), tokenSource.Token);

        Assert.Equal(tokenSource.Token, orderRepository.LastGetLatestOrderNumberToken);
        Assert.Equal(tokenSource.Token, productRepository.LastGetByIdToken);
        Assert.Equal(tokenSource.Token, categoryRepository.LastGetByIdToken);
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
    public async Task GetOrderResultsAsync_GroupsSalesByCurrentDayAndBuildsComparisons()
    {
        var dateTimeProvider = new FakeDateTimeProvider
        {
            UtcNow = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc)
        };
        var deliveredCounter = CreateDeliveredOrder(
            "260820-0001",
            createdAtUtc: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            totalItems: [new OrderItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Latte", 7.5m, 3m, 2)]);
        var deliveredPreviousDay = CreateDeliveredOrder(
            "260819-0002",
            createdAtUtc: new DateTime(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc),
            totalItems:
            [
                new OrderItem(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Brownie", 5m, 2m, 3),
                new OrderItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Latte", 7.5m, 3m, 1),
            ]);
        var pending = CreatePendingOrder("260820-0003", createdAtUtc: new DateTime(2026, 8, 20, 11, 0, 0, DateTimeKind.Utc));
        var cancelled = CreateCancelledOrder("260820-0004", createdAtUtc: new DateTime(2026, 8, 20, 10, 30, 0, DateTimeKind.Utc));
        var orderRepository = new FakeOrderRepository();
        orderRepository.Seed(deliveredCounter, deliveredPreviousDay, pending, cancelled);
        var service = CreateOrderService(orderRepository, new FakeProductRepository(), dateTimeProvider);

        var result = await service.GetOrderResultsAsync();

        Assert.Equal(OrderResultsGroupBy.Daily, result.GroupBy);
        Assert.Equal(new DateOnly(2026, 8, 20), result.PeriodStartDate);
        Assert.Equal(new DateOnly(2026, 8, 20), result.PeriodEndDate);
        Assert.Equal(15m, result.CurrentPeriod.TotalRevenue);
        Assert.Equal(6m, result.CurrentPeriod.TotalCost);
        Assert.Equal(9m, result.CurrentPeriod.TotalProfit);
        Assert.Equal(3, result.CurrentPeriod.TotalOrdersCount);
        Assert.Equal(1, result.CurrentPeriod.DeliveredOrdersCount);
        Assert.Equal(1, result.CurrentPeriod.CancelledOrdersCount);
        Assert.Equal(2, result.CurrentPeriod.DeliveredItemsCount);
        Assert.Equal(15m, result.CurrentPeriod.AverageDeliveredOrderTotal);
        Assert.Equal(22.5m, result.PreviousPeriod.TotalRevenue);
        Assert.Equal(1, result.PreviousPeriod.DeliveredOrdersCount);
        Assert.Equal(4, result.OperationalSnapshot.TotalOrdersCount);
        Assert.Equal(1, result.OperationalSnapshot.ActiveOrdersCount);
        Assert.Equal(1, result.OperationalSnapshot.PendingOrdersCount);
        Assert.Equal(0, result.OperationalSnapshot.PreparingOrdersCount);
        Assert.Equal(0, result.OperationalSnapshot.ReadyOrdersCount);
        Assert.Equal(2, result.OperationalSnapshot.DeliveredOrdersCount);
        Assert.Equal(1, result.OperationalSnapshot.CancelledOrdersCount);
        Assert.Collection(
            result.TopSellingProducts,
            first =>
            {
                Assert.Equal("Latte", first.ProductName);
                Assert.Equal(2, first.QuantitySold);
                Assert.Equal(15m, first.Revenue);
            });
        Assert.Equal(7, result.History.Count);
        var latestHistoryPoint = result.History.Last();
        Assert.Equal(new DateOnly(2026, 8, 20), latestHistoryPoint.StartDate);
        Assert.Equal(15m, latestHistoryPoint.TotalRevenue);
        Assert.Equal(1, latestHistoryPoint.DeliveredOrdersCount);
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
            first =>
            {
                Assert.Equal("260721-0001", first.OrderNumber);
                Assert.NotEmpty(first.Items);
            },
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
        FakeOrderEventPublisher? eventPublisher = null,
        FakeProductCategoryRepository? productCategoryRepository = null)
    {
        productCategoryRepository ??= CreateActiveCategoryRepository(productRepository.Products);

        return new OrderService(
            orderRepository,
            productRepository,
            productCategoryRepository,
            dateTimeProvider ?? new FakeDateTimeProvider
            {
                UtcNow = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc)
            },
            eventPublisher ?? new FakeOrderEventPublisher(() => orderRepository.SaveChangesCalls));
    }

    private static FakeProductCategoryRepository CreateActiveCategoryRepository(
        IReadOnlyCollection<Product> products)
    {
        var repository = new FakeProductCategoryRepository();
        foreach (var categoryId in products.Select(product => product.CategoryId).Distinct())
        {
            repository.SeedForId(categoryId, new ProductCategory("Active category"));
        }

        return repository;
    }

    private static Order CreatePendingOrder(
        string orderNumber,
        DateTime? createdAtUtc = null,
        params OrderItem[] totalItems)
    {
        return new Order(
            orderNumber,
            createdAtUtc ?? new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            totalItems.Length == 0 ? [new OrderItem(Guid.NewGuid(), "Latte", 7.25m, 1)] : totalItems);
    }

    private static Order CreatePreparingOrder(
        string orderNumber,
        DateTime? createdAtUtc = null,
        params OrderItem[] totalItems)
    {
        var order = CreatePendingOrder(orderNumber, createdAtUtc, totalItems);
        order.StartPreparing(order.CreatedAtUtc.AddMinutes(2));
        return order;
    }

    private static Order CreateReadyOrder(
        string orderNumber,
        DateTime? createdAtUtc = null,
        DateTime? readyAtUtc = null,
        params OrderItem[] totalItems)
    {
        var order = CreatePreparingOrder(orderNumber, createdAtUtc, totalItems);
        order.MarkReady(readyAtUtc ?? order.CreatedAtUtc.AddMinutes(5));
        return order;
    }

    private static Order CreateDeliveredOrder(
        string orderNumber,
        DateTime? createdAtUtc = null,
        params OrderItem[] totalItems)
    {
        var order = CreateReadyOrder(orderNumber, createdAtUtc, totalItems: totalItems);
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
