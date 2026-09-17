using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

public sealed class ProductCategoryRepository : IProductCategoryRepository
{
    private readonly InfinitoCoffeeDbContext _dbContext;

    public ProductCategoryRepository(InfinitoCoffeeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductCategories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProductCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductCategories.AddAsync(category, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
    }
}
