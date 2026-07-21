using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.ProductCategories.Contracts;

public interface IProductCategoryRepository
{
    Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProductCategory>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
