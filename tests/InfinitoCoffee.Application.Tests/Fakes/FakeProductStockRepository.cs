using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeProductStockRepository : IProductStockRepository
{
    private readonly Dictionary<Guid, decimal> _minimums = [];

    public Task ArchiveProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        product.Delete();
        _minimums.Remove(product.Id);
        return Task.CompletedTask;
    }

    public Task AddForProductAsync(Product product, decimal minimumQuantity, CancellationToken cancellationToken = default)
    {
        _minimums.Add(product.Id, minimumQuantity);
        return Task.CompletedTask;
    }

    public Task<decimal?> UpdateForProductAsync(Product product, decimal? minimumQuantity, CancellationToken cancellationToken = default)
    {
        if (!_minimums.TryGetValue(product.Id, out var current)) return Task.FromResult<decimal?>(null);
        _minimums[product.Id] = minimumQuantity ?? current;
        return Task.FromResult<decimal?>(_minimums[product.Id]);
    }

    public Task<decimal?> GetMinimumQuantityAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_minimums.TryGetValue(productId, out var minimum) ? (decimal?)minimum : null);

    public Task<IReadOnlyDictionary<Guid, decimal>> GetMinimumQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
            _minimums.Where(x => productIds.Contains(x.Key)).ToDictionary());
}
