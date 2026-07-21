using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Contracts;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetPickupCandidatesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
