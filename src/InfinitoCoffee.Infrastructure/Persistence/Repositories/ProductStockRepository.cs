using System.Data;
using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Stock;
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

    public async Task AddForProductAsync(Product product, decimal minimumQuantity, CancellationToken cancellationToken = default)
    {
        var normalizedName = product.Name.ToUpperInvariant();
        if (await db.StockItems.AnyAsync(x => !x.IsDeleted && x.NormalizedName == normalizedName, cancellationToken))
            throw new ConflictException("Ya existe un artículo de stock con ese nombre.");

        var item = new StockItem
        {
            Name = product.Name,
            NormalizedName = normalizedName,
            Kind = StockItemKind.FinishedProduct,
            Unit = StockUnit.Unit,
            ProductId = product.Id,
            MinimumQuantity = minimumQuantity
        };
        db.StockItems.Add(item);
        var branchIds = await db.Branches.Select(x => x.Id).ToArrayAsync(cancellationToken);
        foreach (var branchId in branchIds)
            db.StockBalances.Add(new StockBalance
            {
                ItemId = item.Id,
                BranchId = branchId,
                Location = StockLocation.Factory
            });
    }

    public async Task<decimal?> UpdateForProductAsync(Product product, decimal? minimumQuantity, CancellationToken cancellationToken = default)
    {
        var item = await db.StockItems.SingleOrDefaultAsync(x => !x.IsDeleted && x.ProductId == product.Id, cancellationToken);
        if (item is null) return null;

        var normalizedName = product.Name.ToUpperInvariant();
        if (item.NormalizedName != normalizedName &&
            await db.StockItems.AnyAsync(x => !x.IsDeleted && x.Id != item.Id && x.NormalizedName == normalizedName, cancellationToken))
            throw new ConflictException("Ya existe un artículo de stock con ese nombre.");

        item.Name = product.Name;
        item.NormalizedName = normalizedName;
        if (minimumQuantity.HasValue) item.MinimumQuantity = minimumQuantity.Value;
        return item.MinimumQuantity;
    }

    public Task<decimal?> GetMinimumQuantityAsync(Guid productId, CancellationToken cancellationToken = default) =>
        db.StockItems.AsNoTracking().Where(x => !x.IsDeleted && x.ProductId == productId)
            .Select(x => (decimal?)x.MinimumQuantity).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetMinimumQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, decimal>();
        return await db.StockItems.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value))
            .ToDictionaryAsync(x => x.ProductId!.Value, x => x.MinimumQuantity, cancellationToken);
    }
}
