using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly InfinitoCoffeeDbContext _dbContext;

    public OrderRepository(InfinitoCoffeeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled)
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetPickupCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.Status == OrderStatus.Preparing || order.Status == OrderStatus.Ready)
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders.AddAsync(order, cancellationToken).AsTask();
    }

    public Task<string?> GetLatestOrderNumberAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAtUtc)
            .Select(order => order.OrderNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
    }
}
