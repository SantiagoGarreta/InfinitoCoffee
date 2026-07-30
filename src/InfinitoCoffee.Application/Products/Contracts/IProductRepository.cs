using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Products.Contracts;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    void Remove(Product product);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
