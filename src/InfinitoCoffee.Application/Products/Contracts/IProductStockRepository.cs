using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Products.Contracts;

public interface IProductStockRepository
{
    Task AddForProductAsync(Product product, decimal minimumQuantity, CancellationToken cancellationToken = default);

    Task ArchiveProductAsync(Product product, CancellationToken cancellationToken = default);

    Task<decimal?> UpdateForProductAsync(Product product, decimal? minimumQuantity, CancellationToken cancellationToken = default);

    Task<decimal?> GetMinimumQuantityAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, decimal>> GetMinimumQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
}
