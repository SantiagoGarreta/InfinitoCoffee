using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Stock;

internal static class StockLedger
{
    public static async Task PostAsync(InfinitoCoffeeDbContext db, StockOperation operation,
        StockItem item, StockLocation location, decimal delta, CancellationToken ct, int branchId = 1)
    {
        var scopeId = item.Kind == StockItemKind.Ingredient ? 0 : branchId;
        var balance = db.StockBalances.Local.FirstOrDefault(x => x.ItemId == item.Id && x.Location == location && x.BranchId == scopeId)
            ?? await db.StockBalances.SingleAsync(x => x.ItemId == item.Id && x.Location == location && x.BranchId == scopeId, ct);
        var after = balance.Quantity + delta;
        if (after < 0)
            throw new ConflictException($"Stock insuficiente de {item.Name}. Disponible: {balance.Quantity:N3}; requerido: {-delta:N3} ({item.Unit}).");
        StockQuantity.ValidateBase(after, item.Unit, item.Kind == StockItemKind.Ingredient);
        operation.Movements.Add(new StockMovement
        {
            ItemId = item.Id,
            Location = location,
            BranchId = scopeId,
            Before = balance.Quantity,
            Delta = delta,
            After = after
        });
        balance.Quantity = after;
        balance.Revision = Guid.NewGuid();
    }
}
