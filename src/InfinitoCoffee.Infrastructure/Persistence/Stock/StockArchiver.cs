using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Stock;

internal static class StockArchiver
{
    public static async Task ArchiveProductAsync(InfinitoCoffeeDbContext db, Product product, CancellationToken ct)
    {
        await CheckOpenOrdersAsync(db, product.Id, ct);
        var item = await db.StockItems.SingleOrDefaultAsync(x => !x.IsDeleted && x.ProductId == product.Id, ct);
        if (item is not null) await ArchiveItemAsync(db, item, ct);
        product.Delete();
    }

    public static async Task ArchiveItemAsync(InfinitoCoffeeDbContext db, StockItem item, CancellationToken ct)
    {
        if (await db.StockBalances.AnyAsync(x => x.ItemId == item.Id && x.Quantity != 0, ct))
            throw new ConflictException("Ajustá el stock de todas las sucursales a cero antes de eliminar el artículo.");

        if (item.Kind == StockItemKind.Ingredient)
        {
            var usedInCurrentRecipe = await db.StockRecipes.AnyAsync(recipe =>
                !recipe.IsArchived && recipe.Lines.Any(line => line.ItemId == item.Id)
                && db.StockItems.Any(output => output.Id == recipe.ItemId && !output.IsDeleted)
                && !db.StockRecipes.Any(newer => newer.ItemId == recipe.ItemId && newer.Version > recipe.Version), ct);
            if (usedInCurrentRecipe)
                throw new ConflictException("Actualizá las recetas que usan este ingrediente antes de eliminarlo.");
        }

        if (item.ProductId.HasValue)
        {
            await CheckOpenOrdersAsync(db, item.ProductId.Value, ct);
            var product = await db.Products.SingleAsync(x => x.Id == item.ProductId.Value, ct);
            product.Delete();
        }

        item.IsDeleted = true;
    }

    private static async Task CheckOpenOrdersAsync(InfinitoCoffeeDbContext db, Guid productId, CancellationToken ct)
    {
        var hasOpenOrders = await db.OrderItems.AnyAsync(line => line.ProductId == productId
            && db.Orders.Any(order => order.Id == line.OrderId
                && (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Preparing
                    || order.Status == OrderStatus.Ready)), ct);
        if (hasOpenOrders)
            throw new ConflictException("Este producto tiene pedidos en curso. Terminá o cancelá esos pedidos antes de eliminarlo.");
    }
}
