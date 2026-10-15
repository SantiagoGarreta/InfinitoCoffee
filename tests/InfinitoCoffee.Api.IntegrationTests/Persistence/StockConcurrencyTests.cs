using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public sealed class StockConcurrencyTests
{
    [Fact]
    public async Task SaleOperation_CanOnlyHaveOneReversalAtDatabaseLevel()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        Guid orderId;
        Guid saleId;

        await using (var setup = factory.CreateDbContext())
        {
            var order = new Order("1", DateTime.UtcNow,
                [new OrderItem(Guid.NewGuid(), "Latte", 100m, 40m, 1)]);
            setup.Orders.Add(order);
            var sale = new StockOperation
            {
                Id = Guid.NewGuid(),
                BranchId = 1,
                Type = "Sale",
                CreatedAtUtc = DateTime.UtcNow,
                OrderId = order.Id,
                Notes = "Venta"
            };
            setup.StockOperations.Add(sale);
            await setup.SaveChangesAsync();
            orderId = order.Id;
            saleId = sale.Id;
        }

        await using (var firstReversal = factory.CreateDbContext())
        {
            firstReversal.StockOperations.Add(new StockOperation
            {
                Id = Guid.NewGuid(),
                BranchId = 1,
                Type = "SaleReversal",
                CreatedAtUtc = DateTime.UtcNow,
                OrderId = orderId,
                ReversesOperationId = saleId,
                Notes = "Primera"
            });
            await firstReversal.SaveChangesAsync();
        }

        await using var duplicate = factory.CreateDbContext();
        duplicate.StockOperations.Add(new StockOperation
        {
            Id = Guid.NewGuid(),
            BranchId = 1,
            Type = "SaleReversal",
            CreatedAtUtc = DateTime.UtcNow,
            OrderId = orderId,
            ReversesOperationId = saleId,
            Notes = "Segunda"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentBalanceUpdates_CannotSilentlyOverwriteAnotherMovement()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var first = factory.CreateDbContext();
        var item = new StockItem { Name = "Huevos", NormalizedName = "HUEVOS", Kind = StockItemKind.Ingredient, Unit = StockUnit.Unit };
        first.StockItems.Add(item);
        first.StockBalances.Add(new StockBalance { ItemId = item.Id, Location = StockLocation.Factory, Quantity = 20 });
        await first.SaveChangesAsync();
        await using var second = factory.CreateDbContext();
        var firstBalance = await first.StockBalances.SingleAsync();
        var staleBalance = await second.StockBalances.SingleAsync();
        firstBalance.Quantity = 10;
        firstBalance.Revision = Guid.NewGuid();
        await first.SaveChangesAsync();
        staleBalance.Quantity = 18;
        staleBalance.Revision = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = factory.CreateDbContext();
        Assert.Equal(10, (await check.StockBalances.SingleAsync()).Quantity);
    }
}
