using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeProductCategoryRepository : IProductCategoryRepository
{
    private readonly List<ProductCategory> _categories = [];

    public int SaveChangesCalls { get; private set; }

    public CancellationToken? LastGetByIdToken { get; private set; }

    public CancellationToken? LastGetAllToken { get; private set; }

    public CancellationToken? LastAddToken { get; private set; }

    public CancellationToken? LastSaveChangesToken { get; private set; }

    public Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastGetByIdToken = cancellationToken;
        return Task.FromResult(_categories.SingleOrDefault(category => category.Id == id));
    }

    public Task<IReadOnlyCollection<ProductCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        LastGetAllToken = cancellationToken;
        return Task.FromResult<IReadOnlyCollection<ProductCategory>>(_categories.ToArray());
    }

    public Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        LastAddToken = cancellationToken;
        _categories.Add(category);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        LastSaveChangesToken = cancellationToken;
        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public void Seed(params ProductCategory[] categories)
    {
        _categories.AddRange(categories);
    }
}
