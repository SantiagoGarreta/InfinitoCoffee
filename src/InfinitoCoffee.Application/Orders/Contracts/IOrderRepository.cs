using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Orders.Contracts;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetPickupCandidatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetHistoryAsync(
        int take,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task<string?> GetLatestOrderNumberAsync(CancellationToken cancellationToken = default);

    Task<Order?> VoidSaleAsync(
        Guid orderId,
        Guid actorId,
        DateTime voidedAtUtc,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
