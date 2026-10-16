using System.Data;
using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using InfinitoCoffee.Infrastructure.Persistence.Stock;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

public sealed class ProductStockRepository(InfinitoCoffeeDbContext db) : IProductStockRepository
{
    public async Task ArchiveProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await StockArchiver.ArchiveProductAsync(db, product, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddForProductAsync(Product product, decimal minimumQuantity, ProductStockMode mode, CancellationToken cancellationToken = default)
    {
        var normalizedName = product.Name.ToUpperInvariant();
        if (await db.StockItems.AnyAsync(x => !x.IsDeleted && x.NormalizedName == normalizedName, cancellationToken))
            throw new ConflictException("Ya existe un artículo de stock con ese nombre.");

        var item = new StockItem
        {
            Name = product.Name,
            NormalizedName = normalizedName,
            Kind = ToItemKind(mode),
            Unit = StockUnit.Unit,
            ProductId = product.Id,
            MinimumQuantity = mode == ProductStockMode.Stocked ? minimumQuantity : 0m
        };
        db.StockItems.Add(item);
        if (mode == ProductStockMode.Stocked)
            await AddBalancesAsync(item.Id, cancellationToken);
    }

    public async Task<decimal?> UpdateForProductAsync(Product product, decimal? minimumQuantity, ProductStockMode? mode, CancellationToken cancellationToken = default)
    {
        var item = await db.StockItems.SingleOrDefaultAsync(x => !x.IsDeleted && x.ProductId == product.Id, cancellationToken);
        if (item is null)
        {
            var initialMinimum = minimumQuantity ?? 0m;
            await AddForProductAsync(product, initialMinimum, mode ?? ProductStockMode.Stocked, cancellationToken);
            return mode == ProductStockMode.MadeToOrder ? null : initialMinimum;
        }

        var normalizedName = product.Name.ToUpperInvariant();
        if (item.NormalizedName != normalizedName &&
            await db.StockItems.AnyAsync(x => !x.IsDeleted && x.Id != item.Id && x.NormalizedName == normalizedName, cancellationToken))
            throw new ConflictException("Ya existe un artículo de stock con ese nombre.");

        item.Name = product.Name;
        item.NormalizedName = normalizedName;
        if (mode.HasValue && item.Kind != ToItemKind(mode.Value))
        {
            var hasOpenOrders = await db.OrderItems.AnyAsync(line => line.ProductId == product.Id
                && db.Orders.Any(order => order.Id == line.OrderId
                    && (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Preparing
                        || order.Status == OrderStatus.Ready)), cancellationToken);
            if (hasOpenOrders)
                throw new ConflictException("Terminá o cancelá los pedidos en curso antes de cambiar el control de stock.");

            if (mode.Value == ProductStockMode.MadeToOrder)
            {
                var balances = await db.StockBalances.Where(x => x.ItemId == item.Id).ToArrayAsync(cancellationToken);
                if (balances.Any(x => x.Quantity != 0))
                    throw new ConflictException("Dejá el stock en cero en todas las sucursales antes de cambiar a preparado al momento.");
                db.StockBalances.RemoveRange(balances);
                item.MinimumQuantity = 0m;
            }
            else
            {
                await AddBalancesAsync(item.Id, cancellationToken);
            }
            item.Kind = ToItemKind(mode.Value);
        }
        if (item.Kind == StockItemKind.MadeToOrder)
        {
            if (minimumQuantity is > 0m)
                throw new ArgumentException("Un producto preparado al momento no tiene stock mínimo.");
            return null;
        }
        if (minimumQuantity.HasValue) item.MinimumQuantity = minimumQuantity.Value;
        return item.MinimumQuantity;
    }

    public async Task<ProductStockMode> GetModeAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var kind = await db.StockItems.AsNoTracking().Where(x => !x.IsDeleted && x.ProductId == productId)
            .Select(x => (StockItemKind?)x.Kind).SingleOrDefaultAsync(cancellationToken);
        return kind == StockItemKind.MadeToOrder ? ProductStockMode.MadeToOrder : ProductStockMode.Stocked;
    }

    public async Task<IReadOnlyDictionary<Guid, ProductStockMode>> GetModesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, ProductStockMode>();
        return await db.StockItems.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value))
            .ToDictionaryAsync(x => x.ProductId!.Value,
                x => x.Kind == StockItemKind.MadeToOrder ? ProductStockMode.MadeToOrder : ProductStockMode.Stocked,
                cancellationToken);
    }

    public Task<decimal?> GetMinimumQuantityAsync(Guid productId, CancellationToken cancellationToken = default) =>
        db.StockItems.AsNoTracking().Where(x => !x.IsDeleted && x.ProductId == productId && x.Kind == StockItemKind.FinishedProduct)
            .Select(x => (decimal?)x.MinimumQuantity).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetMinimumQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, decimal>();
        return await db.StockItems.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Kind == StockItemKind.FinishedProduct && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value))
            .ToDictionaryAsync(x => x.ProductId!.Value, x => x.MinimumQuantity, cancellationToken);
    }

    private async Task AddBalancesAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var branchIds = await db.Branches.Select(x => x.Id).ToArrayAsync(cancellationToken);
        foreach (var branchId in branchIds)
            db.StockBalances.Add(new StockBalance
            {
                ItemId = itemId,
                BranchId = branchId,
                Location = StockLocation.Factory
            });
    }

    private static StockItemKind ToItemKind(ProductStockMode mode) => mode switch
    {
        ProductStockMode.Stocked => StockItemKind.FinishedProduct,
        ProductStockMode.MadeToOrder => StockItemKind.MadeToOrder,
        _ => throw new ArgumentException("Elegí un modo de stock válido.")
    };
}
