using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public class RepositoryPersistenceTests : IDisposable
{
    private readonly SqliteInMemoryDbContextFactory _dbContextFactory = new();

    [Fact]
    public async Task ProductCategory_Persisted_SavesCategory()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new ProductCategoryRepository(dbContext);
        var category = new ProductCategory("Coffee");

        await repository.AddAsync(category);
        await repository.SaveChangesAsync();

        var persisted = await dbContext.ProductCategories.SingleAsync();
        Assert.Equal("Coffee", persisted.Name);
    }

    [Fact]
    public async Task Product_PersistedWithCategory_SavesProduct()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var category = new ProductCategory("Coffee");
        dbContext.ProductCategories.Add(category);
        await dbContext.SaveChangesAsync();
        var repository = new ProductRepository(dbContext);
        var product = new Product(category.Id, "Latte", 7.25m, "Oat milk");

        await repository.AddAsync(product);
        await repository.SaveChangesAsync();

        var persisted = await dbContext.Products.SingleAsync();
        Assert.Equal(category.Id, persisted.CategoryId);
        Assert.Equal("Latte", persisted.Name);
    }

    [Fact]
    public async Task Order_PersistedWithItems_PreservesSnapshots()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new OrderRepository(dbContext);
        var order = new Order(
            "260721-0001",
            OrderSource.Counter,
            new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            [
                new OrderItem(Guid.NewGuid(), "Latte", 7.25m, 2, "Extra hot"),
                new OrderItem(Guid.NewGuid(), "Cookie", 3m, 1)
            ]);

        await repository.AddAsync(order);
        await repository.SaveChangesAsync();

        var persisted = await repository.GetByIdAsync(order.Id);

        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Items.Count);
        Assert.Contains(persisted.Items, item => item.ProductNameSnapshot == "Latte" && item.UnitPriceSnapshot == 7.25m);
        Assert.Contains(persisted.Items, item => item.ProductNameSnapshot == "Cookie" && item.UnitPriceSnapshot == 3m);
    }

    [Fact]
    public async Task GetActiveAsync_WithDeliveredAndCancelled_ExcludesInactiveOrders()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new OrderRepository(dbContext);
        dbContext.Orders.AddRange(
            CreatePendingOrder("260721-0003"),
            CreateDeliveredOrder("260721-0001"),
            CreateCancelledOrder("260721-0002"));
        await dbContext.SaveChangesAsync();

        var result = await repository.GetActiveAsync();

        Assert.Single(result);
        Assert.Equal("260721-0003", result.Single().OrderNumber);
    }

    [Fact]
    public async Task GetPickupCandidatesAsync_WithMixedStatuses_ReturnsPreparingAndReady()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new OrderRepository(dbContext);
        dbContext.Orders.AddRange(
            CreatePreparingOrder("260721-0001"),
            CreateReadyOrder("260721-0002"),
            CreateDeliveredOrder("260721-0003"));
        await dbContext.SaveChangesAsync();

        var result = await repository.GetPickupCandidatesAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, order => Assert.True(
            order.Status == OrderStatus.Preparing || order.Status == OrderStatus.Ready));
    }

    [Fact]
    public async Task OrderNumber_Duplicate_ThrowsUniqueConstraintError()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.Orders.Add(CreatePendingOrder("260721-0001"));
        dbContext.Orders.Add(CreatePendingOrder("260721-0001"));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ProductCategory_DeleteWithProducts_UsesRestrictBehavior()
    {
        Guid categoryId;

        await using (var setupContext = _dbContextFactory.CreateDbContext())
        {
            var category = new ProductCategory("Coffee");
            setupContext.ProductCategories.Add(category);
            setupContext.Products.Add(new Product(category.Id, "Latte", 7.25m));
            await setupContext.SaveChangesAsync();
            categoryId = category.Id;
        }

        await using var deletionContext = _dbContextFactory.CreateDbContext();
        var categoryToDelete = await deletionContext.ProductCategories.SingleAsync(category => category.Id == categoryId);

        deletionContext.ProductCategories.Remove(categoryToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => deletionContext.SaveChangesAsync());
    }

    [Fact]
    public async Task TrackedOrder_TransitionAndSave_PersistsTimestamp()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new OrderRepository(dbContext);
        var order = CreatePendingOrder("260721-0001");
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var trackedOrder = await repository.GetByIdAsync(order.Id);
        trackedOrder!.StartPreparing(new DateTime(2026, 7, 21, 12, 5, 0, DateTimeKind.Utc));
        await repository.SaveChangesAsync();

        await using var verificationContext = _dbContextFactory.CreateDbContext();
        var persisted = await verificationContext.Orders.SingleAsync(saved => saved.Id == order.Id);
        Assert.Equal(new DateTime(2026, 7, 21, 12, 5, 0, DateTimeKind.Utc), persisted.StartedAtUtc);
    }

    [Fact]
    public async Task Order_TimestampsPersisted_PreservesAllValues()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var order = CreateReadyOrder("260721-0001");
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var persisted = await dbContext.Orders.SingleAsync();
        Assert.Equal(order.CreatedAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(order.StartedAtUtc, persisted.StartedAtUtc);
        Assert.Equal(order.ReadyAtUtc, persisted.ReadyAtUtc);
    }

    [Fact]
    public async Task Order_PrivateCollectionMapping_LoadsItemsCorrectly()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var order = new Order(
            "260721-0001",
            OrderSource.Counter,
            new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            [new OrderItem(Guid.NewGuid(), "Latte", 7.25m, 1)]);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var persisted = await dbContext.Orders
            .Include(saved => saved.Items)
            .SingleAsync();

        Assert.Single(persisted.Items);
    }

    [Fact]
    public async Task Repositories_WithCancellationToken_RespectCancellationToken()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new ProductCategoryRepository(dbContext);
        using var cancellationTokenSource = new CancellationTokenSource();

        await repository.AddAsync(new ProductCategory("Coffee"), cancellationTokenSource.Token);
        await repository.SaveChangesAsync(cancellationTokenSource.Token);
        var result = await repository.GetAllAsync(cancellationTokenSource.Token);

        Assert.Single(result);
    }

    [Fact]
    public async Task SaveChanges_WithArtificialConcurrencyException_TranslatesToConflictException()
    {
        var options = new DbContextOptionsBuilder<InfinitoCoffee.Infrastructure.Persistence.InfinitoCoffeeDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var dbContext = new TestConcurrencyDbContext(options);
        var repository = new ConcurrencyThrowingProductRepository(dbContext);

        await Assert.ThrowsAsync<ConflictException>(() => repository.TriggerSaveAsync());
    }

    public void Dispose()
    {
        _dbContextFactory.Dispose();
    }

    private static Order CreatePendingOrder(string orderNumber)
    {
        return new Order(
            orderNumber,
            OrderSource.Counter,
            new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            [new OrderItem(Guid.NewGuid(), "Latte", 7.25m, 1)]);
    }

    private static Order CreatePreparingOrder(string orderNumber)
    {
        var order = CreatePendingOrder(orderNumber);
        order.StartPreparing(order.CreatedAtUtc.AddMinutes(2));
        return order;
    }

    private static Order CreateReadyOrder(string orderNumber)
    {
        var order = CreatePreparingOrder(orderNumber);
        order.MarkReady(order.CreatedAtUtc.AddMinutes(5));
        return order;
    }

    private static Order CreateDeliveredOrder(string orderNumber)
    {
        var order = CreateReadyOrder(orderNumber);
        order.Deliver(order.ReadyAtUtc!.Value.AddMinutes(2));
        return order;
    }

    private static Order CreateCancelledOrder(string orderNumber)
    {
        var order = CreatePendingOrder(orderNumber);
        order.Cancel(order.CreatedAtUtc.AddMinutes(4));
        return order;
    }

    private sealed class ConcurrencyThrowingProductRepository : ProductRepository
    {
        private readonly TestConcurrencyDbContext _dbContext;

        public ConcurrencyThrowingProductRepository(TestConcurrencyDbContext dbContext)
            : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public Task TriggerSaveAsync()
        {
            return SaveChangesAsync();
        }
    }

    private sealed class TestConcurrencyDbContext : InfinitoCoffee.Infrastructure.Persistence.InfinitoCoffeeDbContext
    {
        public TestConcurrencyDbContext(DbContextOptions<InfinitoCoffee.Infrastructure.Persistence.InfinitoCoffeeDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateConcurrencyException("Forced concurrency exception.");
        }
    }
}
