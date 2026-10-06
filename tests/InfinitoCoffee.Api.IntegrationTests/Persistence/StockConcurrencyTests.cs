using InfinitoCoffee.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public sealed class StockConcurrencyTests
{
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
