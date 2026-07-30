using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly List<Product> _products = [];

    public int SaveChangesCalls { get; private set; }

    public CancellationToken? LastGetByIdToken { get; private set; }

    public CancellationToken? LastGetAllToken { get; private set; }

    public CancellationToken? LastAddToken { get; private set; }

    public CancellationToken? LastSaveChangesToken { get; private set; }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastGetByIdToken = cancellationToken;
        return Task.FromResult(_products.SingleOrDefault(product => product.Id == id));
    }

    public Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        LastGetAllToken = cancellationToken;
        return Task.FromResult<IReadOnlyCollection<Product>>(_products.ToArray());
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        LastAddToken = cancellationToken;
        _products.Add(product);
        return Task.CompletedTask;
    }

    public void Remove(Product product)
    {
        _products.Remove(product);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        LastSaveChangesToken = cancellationToken;
        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public void Seed(params Product[] products)
    {
        _products.AddRange(products);
    }
}
