using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Products.Contracts;

public interface IProductStockRepository
{
    Task AddForProductAsync(Product product, decimal minimumQuantity, ProductStockMode mode, CancellationToken cancellationToken = default, decimal? minimumQuantityBranch2 = null);

    Task ArchiveProductAsync(Product product, CancellationToken cancellationToken = default);

    Task<decimal?> UpdateForProductAsync(Product product, decimal? minimumQuantity, ProductStockMode? mode, CancellationToken cancellationToken = default, decimal? minimumQuantityBranch2 = null);

    Task<ProductStockMode> GetModeAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProductStockMode>> GetModesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);

    Task<decimal?> GetMinimumQuantityAsync(Guid productId, CancellationToken cancellationToken = default, int branchId = 1);

    Task<IReadOnlyDictionary<Guid, decimal>> GetMinimumQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default, int branchId = 1);
}
