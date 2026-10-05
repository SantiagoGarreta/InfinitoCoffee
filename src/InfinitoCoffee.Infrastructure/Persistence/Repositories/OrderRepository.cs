using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using System.Data;
using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Infrastructure.Persistence.Stock;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly InfinitoCoffeeDbContext _dbContext;
    private readonly Application.Branches.IBranchContext _branch;
    private IQueryable<Order> BranchOrders => _dbContext.Orders.Where(x => x.BranchId == _branch.BranchId);

    public OrderRepository(InfinitoCoffeeDbContext dbContext, Application.Branches.IBranchContext? branch = null)
    {
        _dbContext = dbContext;
        _branch = branch ?? new Application.Branches.BranchContext();
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BranchOrders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await BranchOrders
            .AsNoTracking()
            .Include(order => order.Items)
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await BranchOrders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled)
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetPickupCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await BranchOrders
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
        return BranchOrders
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAtUtc)
            .Select(order => order.OrderNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var created = _dbContext.ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .Where(order => order.Items.Any(item => item.SoldAtUtc is not null))
            .ToArray();
        var delivered = _dbContext.ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Modified
                && entry.Entity.Status == OrderStatus.Delivered
                && entry.Property(x => x.Status).OriginalValue != OrderStatus.Delivered)
            .Select(entry => entry.Entity).ToArray();
        if (delivered.Length == 0 && created.Length == 0)
        {
            await EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
            return;
        }

        // Each sale stage and its stock consumption commit together.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        foreach (var order in created.Concat(delivered))
        {
            var soldAtCreation = created.Contains(order);
            var soldItems = soldAtCreation
                ? order.Items.Where(item => item.SoldAtUtc is not null).ToArray()
                : order.Items.Where(item => item.SoldAtUtc is null).ToArray();
            var productIds = soldItems.Select(x => x.ProductId).Distinct().ToArray();
            var trackedItems = await _dbContext.StockItems
                .Where(x => x.ProductId.HasValue && productIds.Contains(x.ProductId.Value)).ToListAsync(cancellationToken);
            if (trackedItems.Count == 0) continue;
            var operation = new StockOperation
            {
                Id = Guid.NewGuid(),
                Type = "Sale",
                BranchId = order.BranchId,
                OrderId = order.Id,
                CreatedAtUtc = soldAtCreation ? order.CreatedAtUtc : order.DeliveredAtUtc!.Value,
                Notes = soldAtCreation ? $"Venta en caja del pedido {order.OrderNumber}" : $"Entrega del pedido {order.OrderNumber}"
            };
            _dbContext.StockOperations.Add(operation);
            foreach (var item in trackedItems.OrderBy(x => x.Id))
            {
                var quantity = soldItems.Where(x => x.ProductId == item.ProductId).Sum(x => x.Quantity);
                await StockLedger.PostAsync(_dbContext, operation, item, StockLocation.Factory, -quantity, cancellationToken, order.BranchId);
            }
        }
        await EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
