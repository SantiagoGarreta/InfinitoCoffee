using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeProductStockRepository : IProductStockRepository
{
    private readonly Dictionary<Guid, decimal> _minimums = [];
    private readonly Dictionary<Guid, ProductStockMode> _modes = [];

    public Task ArchiveProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        product.Delete();
        _minimums.Remove(product.Id);
        _modes.Remove(product.Id);
        return Task.CompletedTask;
    }

    public Task AddForProductAsync(Product product, decimal minimumQuantity, ProductStockMode mode, CancellationToken cancellationToken = default)
    {
        _modes.Add(product.Id, mode);
        if (mode == ProductStockMode.Stocked) _minimums.Add(product.Id, minimumQuantity);
        return Task.CompletedTask;
    }

    public Task<decimal?> UpdateForProductAsync(Product product, decimal? minimumQuantity, ProductStockMode? mode, CancellationToken cancellationToken = default)
    {
        var nextMode = mode ?? (_modes.TryGetValue(product.Id, out var existingMode) ? existingMode : ProductStockMode.Stocked);
        _modes[product.Id] = nextMode;
        if (nextMode == ProductStockMode.MadeToOrder)
        {
            _minimums.Remove(product.Id);
            return Task.FromResult<decimal?>(null);
        }
        if (!_minimums.TryGetValue(product.Id, out var current))
        {
            _minimums[product.Id] = minimumQuantity ?? 0m;
            return Task.FromResult<decimal?>(_minimums[product.Id]);
        }
        _minimums[product.Id] = minimumQuantity ?? current;
        return Task.FromResult<decimal?>(_minimums[product.Id]);
    }

    public Task<decimal?> GetMinimumQuantityAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_minimums.TryGetValue(productId, out var minimum) ? (decimal?)minimum : null);

    public Task<ProductStockMode> GetModeAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_modes.TryGetValue(productId, out var mode) ? mode : ProductStockMode.Stocked);

    public Task<IReadOnlyDictionary<Guid, ProductStockMode>> GetModesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ProductStockMode>>(
            _modes.Where(x => productIds.Contains(x.Key)).ToDictionary());

    public Task<IReadOnlyDictionary<Guid, decimal>> GetMinimumQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
            _minimums.Where(x => productIds.Contains(x.Key)).ToDictionary());
}
