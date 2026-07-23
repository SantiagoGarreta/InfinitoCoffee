using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly InfinitoCoffeeDbContext _dbContext;

    public ProductRepository(InfinitoCoffeeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products.SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        return _dbContext.Products.AddAsync(product, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
    }
}
