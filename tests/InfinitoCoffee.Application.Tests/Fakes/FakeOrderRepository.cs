using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];

    public int SaveChangesCalls { get; private set; }

    public CancellationToken? LastGetByIdToken { get; private set; }

    public CancellationToken? LastGetAllToken { get; private set; }

    public CancellationToken? LastGetActiveToken { get; private set; }

    public CancellationToken? LastGetPickupCandidatesToken { get; private set; }

    public CancellationToken? LastGetHistoryToken { get; private set; }

    public CancellationToken? LastAddToken { get; private set; }

    public CancellationToken? LastGetLatestOrderNumberToken { get; private set; }

    public CancellationToken? LastSaveChangesToken { get; private set; }

    public Exception? SaveChangesException { get; set; }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        LastGetByIdToken = cancellationToken;
        return Task.FromResult(_orders.SingleOrDefault(order => order.Id == id));
    }

    public Task<IReadOnlyCollection<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        LastGetAllToken = cancellationToken;
        return Task.FromResult<IReadOnlyCollection<Order>>(_orders.ToArray());
    }

    public Task<IReadOnlyCollection<Order>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        LastGetActiveToken = cancellationToken;
        return Task.FromResult<IReadOnlyCollection<Order>>(_orders.Where(order => order.IsActive).ToArray());
    }

    public Task<IReadOnlyCollection<Order>> GetPickupCandidatesAsync(CancellationToken cancellationToken = default)
    {
        LastGetPickupCandidatesToken = cancellationToken;
        return Task.FromResult<IReadOnlyCollection<Order>>(
            _orders.Where(order => order.Status is OrderStatus.Preparing or OrderStatus.Ready).ToArray());
    }

    public Task<IReadOnlyCollection<Order>> GetHistoryAsync(
        int take,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeId,
        CancellationToken cancellationToken = default)
    {
        LastGetHistoryToken = cancellationToken;
        var orders = _orders
            .Where(order => !beforeCreatedAtUtc.HasValue
                || order.CreatedAtUtc < beforeCreatedAtUtc.Value
                || (order.CreatedAtUtc == beforeCreatedAtUtc.Value && order.Id.CompareTo(beforeId!.Value) < 0))
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Take(take)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<Order>>(orders);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        LastAddToken = cancellationToken;
        _orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<string?> GetLatestOrderNumberAsync(CancellationToken cancellationToken = default)
    {
        LastGetLatestOrderNumberToken = cancellationToken;
        return Task.FromResult(
            _orders
                .OrderByDescending(order => order.CreatedAtUtc)
                .Select(order => order.OrderNumber)
                .FirstOrDefault());
    }

    public Task<Order?> VoidSaleAsync(
        Guid orderId,
        Guid actorId,
        DateTime voidedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var order = _orders.SingleOrDefault(candidate => candidate.Id == orderId);
        order?.Void(voidedAtUtc);
        return Task.FromResult(order);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        LastSaveChangesToken = cancellationToken;

        if (SaveChangesException is not null)
        {
            throw SaveChangesException;
        }

        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public void Seed(params Order[] orders)
    {
        _orders.AddRange(orders);
    }
}
