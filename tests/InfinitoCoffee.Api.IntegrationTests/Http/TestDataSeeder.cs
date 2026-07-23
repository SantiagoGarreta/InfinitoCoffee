using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Infrastructure.Persistence;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

internal static class TestDataSeeder
{
    public static async Task<Guid> SeedActiveProductAsync(InfinitoCoffeeDbContext dbContext)
    {
        var category = await AddCategoryAsync(dbContext);
        var product = await AddProductAsync(dbContext, category.Id);
        return product.Id;
    }

    public static async Task<Guid> SeedPendingOrderAsync(InfinitoCoffeeDbContext dbContext)
    {
        var order = await AddOrderAsync(
            dbContext,
            "RTR-0001",
            OrderStatus.Pending,
            DateTime.UtcNow.AddMinutes(-2));

        return order.Id;
    }

    public static async Task<Guid> SeedPreparingOrderAsync(InfinitoCoffeeDbContext dbContext)
    {
        var order = await AddOrderAsync(
            dbContext,
            "RTR-0002",
            OrderStatus.Preparing,
            DateTime.UtcNow.AddMinutes(-3));

        return order.Id;
    }

    public static async Task<ProductCategory> AddCategoryAsync(
        InfinitoCoffeeDbContext dbContext,
        string name = "Coffee",
        bool isActive = true)
    {
        var category = new ProductCategory(name);
        if (!isActive)
        {
            category.Deactivate();
        }

        dbContext.ProductCategories.Add(category);
        await dbContext.SaveChangesAsync();
        return category;
    }

    public static async Task<Product> AddProductAsync(
        InfinitoCoffeeDbContext dbContext,
        Guid categoryId,
        string name = "Latte",
        decimal price = 150m,
        bool isActive = true)
    {
        var product = new Product(categoryId, name, price, "Test product");
        if (!isActive)
        {
            product.Deactivate();
        }

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return product;
    }

    public static async Task<Order> AddOrderAsync(
        InfinitoCoffeeDbContext dbContext,
        string orderNumber,
        OrderStatus status,
        DateTime createdAtUtc,
        DateTime? readyAtUtc = null)
    {
        var order = new Order(
            orderNumber,
            OrderSource.Counter,
            createdAtUtc,
            [new OrderItem(Guid.NewGuid(), "Latte", 150m, 1, "No cinnamon")],
            "Test order");

        if (status is OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Delivered)
        {
            order.StartPreparing(createdAtUtc.AddMinutes(1));
        }

        if (status is OrderStatus.Ready or OrderStatus.Delivered)
        {
            order.MarkReady(readyAtUtc ?? createdAtUtc.AddMinutes(2));
        }

        if (status == OrderStatus.Delivered)
        {
            order.Deliver((readyAtUtc ?? createdAtUtc.AddMinutes(2)).AddMinutes(1));
        }

        if (status == OrderStatus.Cancelled)
        {
            order.Cancel(createdAtUtc.AddMinutes(1));
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return order;
    }
}
