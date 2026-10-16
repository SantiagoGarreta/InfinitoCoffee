using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using System.Data;
using InfinitoCoffee.Application.Common.Exceptions;
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
            .Where(order => order.Status == OrderStatus.Pending
                || order.Status == OrderStatus.Preparing
                || order.Status == OrderStatus.Ready)
            .OrderBy(order => order.CreatedAtUtc)
            .ThenBy(order => order.OrderNumber)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetHistoryAsync(
        int take,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeId,
        CancellationToken cancellationToken = default)
    {
        var query = BranchOrders.AsNoTracking().Include(order => order.Items).AsQueryable();

        if (beforeCreatedAtUtc.HasValue && beforeId.HasValue)
        {
            query = query.Where(order => order.CreatedAtUtc < beforeCreatedAtUtc.Value
                || (order.CreatedAtUtc == beforeCreatedAtUtc.Value
                    && order.Id.CompareTo(beforeId.Value) < 0));
        }

        return await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Take(take)
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

    public async Task<Order?> VoidSaleAsync(
        Guid orderId,
        Guid actorId,
        DateTime voidedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var order = await BranchOrders
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        if (order.Status == OrderStatus.Voided)
        {
            throw new ConflictException("El pedido ya fue anulado.");
        }

        order.Void(voidedAtUtc);

        var reversedOperationIds = await ReverseSalesAsync(order, voidedAtUtc, actorId, cancellationToken);

        try
        {
            await EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("El pedido ya fue anulado.");
        }
        catch (DbUpdateException) when (reversedOperationIds.Length > 0)
        {
            throw new ConflictException("El pedido ya fue anulado.");
        }

        return order;
    }

    private async Task<Guid[]> ReverseSalesAsync(
        Order order,
        DateTime reversedAtUtc,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        var sales = await _dbContext.StockOperations
            .Include(operation => operation.Movements)
            .Where(operation => operation.OrderId == order.Id && operation.Type == "Sale")
            .OrderBy(operation => operation.CreatedAtUtc)
            .ThenBy(operation => operation.Id)
            .ToArrayAsync(cancellationToken);
        var reversedOperationIds = sales.Select(operation => operation.Id).ToArray();

        if (reversedOperationIds.Length > 0
            && await _dbContext.StockOperations.AnyAsync(
                operation => operation.ReversesOperationId.HasValue
                    && reversedOperationIds.Contains(operation.ReversesOperationId.Value),
                cancellationToken))
        {
            throw new ConflictException("El pedido ya fue anulado.");
        }

        var movementItemIds = sales
            .SelectMany(operation => operation.Movements)
            .Where(movement => movement.Delta < 0)
            .Select(movement => movement.ItemId)
            .Distinct()
            .ToArray();
        var items = await _dbContext.StockItems
            .Where(item => movementItemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var sale in sales)
        {
            var reversal = new StockOperation
            {
                Id = Guid.NewGuid(),
                BranchId = order.BranchId,
                Type = "SaleReversal",
                CreatedAtUtc = reversedAtUtc,
                ActorId = actorId,
                Notes = order.Status == OrderStatus.Cancelled
                    ? $"Cancelación de venta del pedido {order.OrderNumber}"
                    : $"Anulación de venta del pedido {order.OrderNumber}",
                OrderId = order.Id,
                ReversesOperationId = sale.Id
            };
            _dbContext.StockOperations.Add(reversal);

            foreach (var movement in sale.Movements.OrderBy(movement => movement.ItemId))
            {
                if (movement.Delta >= 0
                    || !items.TryGetValue(movement.ItemId, out var item)
                    || item.Kind != StockItemKind.FinishedProduct)
                {
                    continue;
                }

                await StockLedger.PostAsync(
                    _dbContext,
                    reversal,
                    item,
                    movement.Location,
                    -movement.Delta,
                    cancellationToken,
                    movement.BranchId);
            }
        }

        return reversedOperationIds;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var newOrders = _dbContext.ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToArray();
        var created = newOrders.Where(order => order.Items.Any(item => item.SoldAtUtc is not null)).ToArray();
        var ready = _dbContext.ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Modified
                && entry.Entity.Status == OrderStatus.Ready
                && entry.Property(x => x.Status).OriginalValue != OrderStatus.Ready)
            .Select(entry => entry.Entity).ToArray();
        var delivered = _dbContext.ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Modified
                && entry.Entity.Status == OrderStatus.Delivered
                && entry.Property(x => x.Status).OriginalValue != OrderStatus.Delivered)
            .Select(entry => entry.Entity).ToArray();
        var cancelled = _dbContext.ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Modified
                && entry.Entity.Status == OrderStatus.Cancelled
                && entry.Property(x => x.Status).OriginalValue != OrderStatus.Cancelled)
            .Select(entry => entry.Entity).ToArray();
        if (delivered.Length == 0 && newOrders.Length == 0 && ready.Length == 0 && cancelled.Length == 0)
        {
            await EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
            return;
        }

        // Sales, preparations, cancellations, and their stock movements commit together.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        foreach (var order in newOrders)
            await EnsurePreparedProductsHaveRecipesAsync(order, cancellationToken);
        foreach (var order in cancelled)
        {
            await ReverseSalesAsync(order, order.CancelledAtUtc!.Value, null, cancellationToken);
        }
        foreach (var order in created.Concat(delivered))
        {
            var soldAtCreation = created.Contains(order);
            var soldItems = soldAtCreation
                ? order.Items.Where(item => item.SoldAtUtc is not null).ToArray()
                : order.Items.Where(item => item.SoldAtUtc is null).ToArray();
            if (soldAtCreation)
                await PostPreparationsAsync(order, soldItems, order.CreatedAtUtc, cancellationToken);
            var productIds = soldItems.Select(x => x.ProductId).Distinct().ToArray();
            var trackedItems = await _dbContext.StockItems
                .Where(x => x.Kind == StockItemKind.FinishedProduct && !x.IsDeleted
                    && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value)).ToListAsync(cancellationToken);
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
        foreach (var order in ready)
            await PostPreparationsAsync(order, order.Items.Where(item => item.SoldAtUtc is null),
                order.ReadyAtUtc!.Value, cancellationToken);
        await EfRepositorySaveChanges.SaveAsync(_dbContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsurePreparedProductsHaveRecipesAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToArray();
        var prepared = await _dbContext.StockItems.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Kind == StockItemKind.MadeToOrder
                && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value))
            .ToArrayAsync(cancellationToken);
        foreach (var item in prepared)
            if (!await _dbContext.StockRecipes.AnyAsync(x => x.ItemId == item.Id && !x.IsArchived, cancellationToken))
                throw new ConflictException($"Cargá una receta para {item.Name} antes de venderlo.");
    }

    private async Task PostPreparationsAsync(Order order, IEnumerable<OrderItem> orderItems,
        DateTime preparedAtUtc, CancellationToken cancellationToken)
    {
        var quantities = orderItems.GroupBy(x => x.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
        if (quantities.Count == 0) return;
        var productIds = quantities.Keys.ToArray();
        var preparedItems = await _dbContext.StockItems
            .Where(x => !x.IsDeleted && x.Kind == StockItemKind.MadeToOrder
                && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value))
            .OrderBy(x => x.Id).ToArrayAsync(cancellationToken);
        foreach (var item in preparedItems)
        {
            var recipe = await _dbContext.StockRecipes.Include(x => x.Lines)
                .Where(x => x.ItemId == item.Id && !x.IsArchived).OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ConflictException($"Cargá una receta para {item.Name} antes de prepararlo.");
            var operation = new StockOperation
            {
                Id = Guid.NewGuid(),
                Type = "Preparation",
                BranchId = order.BranchId,
                OrderId = order.Id,
                RecipeId = recipe.Id,
                CreatedAtUtc = preparedAtUtc,
                Notes = $"Preparación de {item.Name} para el pedido {order.OrderNumber}"
            };
            _dbContext.StockOperations.Add(operation);
            foreach (var line in recipe.Lines.OrderBy(x => x.ItemId))
            {
                var ingredient = await _dbContext.StockItems.SingleAsync(x => x.Id == line.ItemId && !x.IsDeleted,
                    cancellationToken);
                var consumed = decimal.Round(line.Quantity * quantities[item.ProductId!.Value] / recipe.Yield,
                    3, MidpointRounding.AwayFromZero);
                if (consumed <= 0)
                    throw new ConflictException($"La receta de {item.Name} requiere más precisión para esta cantidad.");
                StockQuantity.ValidateBase(consumed, ingredient.Unit, true);
                await StockLedger.PostAsync(_dbContext, operation, ingredient, StockLocation.Factory,
                    -consumed, cancellationToken);
            }
        }
    }
}
