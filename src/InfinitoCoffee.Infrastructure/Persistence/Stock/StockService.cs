using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Common.Time;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Stock;

public sealed class StockService(InfinitoCoffeeDbContext db, IDateTimeProvider clock,
    InfinitoCoffee.Application.Branches.IBranchContext? branch = null) : IStockService
{
    private int BranchId => branch?.BranchId ?? 1;
    public async Task<StockDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var items = await db.StockItems.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var balances = await db.StockBalances.AsNoTracking()
            .Where(x => x.Location == StockLocation.Factory && (x.BranchId == 0 || x.BranchId == BranchId)).ToListAsync(ct);
        var recipes = await db.StockRecipes.AsNoTracking().Include(x => x.Lines)
            .Where(x => !db.StockRecipes.Any(newer => newer.ItemId == x.ItemId && newer.Version > x.Version)).ToListAsync(ct);
        return new(items.Select(MapItem).ToArray(),
            balances.Select(x => new StockBalanceDto(x.ItemId, x.Location, x.Quantity, x.Revision, x.BranchId)).ToArray(),
            recipes.Select(MapRecipe).ToArray());
    }

    public async Task<StockItemDto> CreateItemAsync(StockItemRequest request, CancellationToken ct)
    {
        var name = Name(request.Name);
        if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.Unit)) throw new ArgumentException("Tipo o unidad inválida.");
        StockQuantity.ValidateBase(request.MinimumQuantity, request.Unit);
        if (request.Kind == StockItemKind.FinishedProduct && (request.ProductId is null || request.Unit != StockUnit.Unit))
            throw new ArgumentException("Vinculá el producto del catálogo. Los productos terminados se cuentan por unidad.");
        if (request.Kind == StockItemKind.Ingredient && request.ProductId is not null)
            throw new ArgumentException("Un ingrediente no se vincula a un producto de venta.");
        if (request.ProductId is not null && !await db.Products.AnyAsync(x => x.Id == request.ProductId, ct))
            throw new NotFoundException("Product", request.ProductId.Value);
        if (await db.StockItems.AnyAsync(x => x.NormalizedName == name.ToUpperInvariant()
            || (request.ProductId != null && x.ProductId == request.ProductId), ct))
            throw new ConflictException("Ya existe ese artículo o el producto ya tiene control de stock.");
        var item = new StockItem
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Kind = request.Kind,
            Unit = request.Unit,
            ProductId = request.ProductId,
            MinimumQuantity = request.MinimumQuantity
        };
        db.StockItems.Add(item);
        var scopeIds = item.Kind == StockItemKind.Ingredient ? [0] : await db.Branches.Select(x => x.Id).ToArrayAsync(ct);
        foreach (var scopeId in scopeIds)
            db.StockBalances.Add(new StockBalance { ItemId = item.Id, Location = StockLocation.Factory, BranchId = scopeId });
        await db.SaveChangesAsync(ct);
        return MapItem(item);
    }

    public async Task<StockItemDto> UpdateItemAsync(Guid id, StockItemUpdate request, CancellationToken ct)
    {
        var item = await GetItem(id, ct);
        var name = Name(request.Name);
        StockQuantity.ValidateBase(request.MinimumQuantity, item.Unit);
        if (await db.StockItems.AnyAsync(x => x.Id != id && x.NormalizedName == name.ToUpperInvariant(), ct))
            throw new ConflictException("Ya existe un artículo con ese nombre.");
        item.Name = name;
        item.NormalizedName = name.ToUpperInvariant();
        item.MinimumQuantity = request.MinimumQuantity;
        await db.SaveChangesAsync(ct);
        return MapItem(item);
    }

    public async Task<RecipeDto> SaveRecipeAsync(RecipeRequest request, Guid actorId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var product = await GetItem(request.ItemId, ct);
        if (product.Kind != StockItemKind.FinishedProduct) throw new ArgumentException("La receta debe producir un producto terminado.");
        Positive(request.Yield, StockUnit.Unit);
        var lines = await NormalizeLines(request.Lines, false, ct);
        if (lines.Any(x => x.Item.Kind != StockItemKind.Ingredient))
            throw new ArgumentException("Las recetas deben contener ingredientes.");
        var version = (await db.StockRecipes.Where(x => x.ItemId == product.Id).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var recipe = new StockRecipe
        {
            ItemId = product.Id,
            Version = version,
            Yield = request.Yield,
            CreatedAtUtc = clock.UtcNow,
            CreatedBy = actorId,
            Lines = lines.Select(x => new StockRecipeLine { ItemId = x.Item.Id, Quantity = x.Quantity }).ToList()
        };
        db.StockRecipes.Add(recipe);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapRecipe(recipe);
    }

    public Task RecordAdjustmentAsync(StockAdjustmentRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "Adjustment", request, actorId, request.Notes, async op =>
        {
            RequiredReason(request.Notes);
            if (request.Lines is null || request.Lines.Count is < 1 or > 100 ||
                request.Lines.Select(x => x.ItemId).Distinct().Count() != request.Lines.Count)
                throw new ArgumentException("Ingresá entre 1 y 100 artículos sin repetirlos.");
            var changes = new List<(StockItem Item, decimal Delta)>();
            foreach (var line in request.Lines.OrderBy(x => x.ItemId))
            {
                var item = await GetItem(line.ItemId, ct);
                if (line.Delta == 0) throw new ArgumentException("El ajuste debe aumentar o disminuir la cantidad.");
                var amount = StockQuantity.Convert(Math.Abs(line.Delta), line.Unit, item.Unit);
                changes.Add((item, line.Delta < 0 ? -amount : amount));
            }
            // Purchases in the same adjustment are available before a finished product is received.
            foreach (var change in changes.Where(x => x.Item.Kind == StockItemKind.Ingredient && x.Delta > 0))
                await StockLedger.PostAsync(db, op, change.Item, StockLocation.Factory, change.Delta, ct);
            foreach (var change in changes.Where(x => x.Item.Kind == StockItemKind.FinishedProduct && x.Delta > 0))
            {
                var recipe = await db.StockRecipes.Include(x => x.Lines).Where(x => x.ItemId == change.Item.Id)
                    .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
                if (recipe is not null && request.ConsumeIngredients)
                {
                    if (changes.Count == 1) op.RecipeId = recipe.Id;
                    foreach (var line in recipe.Lines.OrderBy(x => x.ItemId))
                    {
                        var ingredient = await GetItem(line.ItemId, ct);
                        var consumed = decimal.Round(line.Quantity * change.Delta / recipe.Yield, 3, MidpointRounding.AwayFromZero);
                        if (consumed <= 0) throw new ArgumentException("La receta requiere más precisión para esta cantidad.");
                        StockQuantity.ValidateBase(consumed, ingredient.Unit, true);
                        await StockLedger.PostAsync(db, op, ingredient, StockLocation.Factory, -consumed, ct);
                    }
                }
                await StockLedger.PostAsync(db, op, change.Item, StockLocation.Factory, change.Delta, ct, BranchId);
            }
            foreach (var change in changes.Where(x => x.Delta < 0))
                await StockLedger.PostAsync(db, op, change.Item, StockLocation.Factory, change.Delta, ct, BranchId);
        }, ct);

    public async Task<StockHistoryDto> GetHistoryAsync(int page, Guid? itemId, CancellationToken ct)
    {
        if (page < 0 || page > 100000) throw new ArgumentException("Página inválida.");
        var query = db.StockOperations.AsNoTracking().Include(x => x.Movements)
            .Where(x => x.Movements.Any(m => m.BranchId == 0 || m.BranchId == BranchId));
        if (itemId.HasValue) query = query.Where(x => x.Movements.Any(m => m.ItemId == itemId && (m.BranchId == 0 || m.BranchId == BranchId)));
        var operations = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip(page * 30).Take(31).ToListAsync(ct);
        var items = await db.StockItems.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var actors = operations.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => actors.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var recipeIds = operations.Where(x => x.RecipeId.HasValue).Select(x => x.RecipeId!.Value).ToArray();
        var versions = await db.StockRecipes.AsNoTracking().Where(x => recipeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Version, ct);
        return new(operations.Take(30).Select(x => new OperationDto(x.Id, x.Type, x.CreatedAtUtc, x.ActorId,
            x.ActorId.HasValue ? users.GetValueOrDefault(x.ActorId.Value, "Administrador") : "Venta automática",
            x.Notes, x.RecipeId, x.RecipeId.HasValue ? versions[x.RecipeId.Value] : null, x.OrderId,
            x.Movements.Where(m => m.BranchId == 0 || m.BranchId == BranchId).Select(m => new MovementDto(m.ItemId, items[m.ItemId].Name,
                items[m.ItemId].Unit, m.Location, m.Before, m.Delta, m.After, m.BranchId)).ToArray(), x.BranchId)).ToArray(), operations.Count > 30);
    }

    private async Task Execute(Guid operationId, string type, object request, Guid actorId, string? notes,
        Func<StockOperation, Task> action, CancellationToken ct)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Falta el identificador de la operación.");
        if ((notes?.Length ?? 0) > 1000) throw new ArgumentException("El motivo admite hasta 1000 caracteres.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { BranchId, Request = request }))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await db.StockOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, ct);
        if (existing is not null)
        {
            if (existing.Type != type || existing.RequestHash != hash || existing.ActorId != actorId)
                throw new ConflictException("El identificador ya corresponde a otra operación.");
            return; // A lost HTTP response can be retried without applying the movement twice.
        }
        var operation = new StockOperation
        {
            Id = operationId,
            Type = type,
            BranchId = BranchId,
            RequestHash = hash,
            CreatedAtUtc = clock.UtcNow,
            ActorId = actorId,
            Notes = notes?.Trim() ?? string.Empty
        };
        db.StockOperations.Add(operation);
        await action(operation);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("El stock cambió durante la operación. Actualizá y reintentá."); }
        await tx.CommitAsync(ct);
    }

    private async Task<List<(StockItem Item, decimal Quantity)>> NormalizeLines(IReadOnlyList<StockLineInput>? lines, bool allowZero, CancellationToken ct)
    {
        if (lines is null || lines.Count is < 1 or > 100 || lines.Any(x => x is null)
            || lines.Select(x => x.ItemId).Distinct().Count() != lines.Count)
            throw new ArgumentException("Ingresá entre 1 y 100 artículos sin repetirlos.");
        var result = new List<(StockItem, decimal)>();
        foreach (var line in lines.OrderBy(x => x.ItemId))
        {
            var item = await GetItem(line.ItemId, ct);
            result.Add((item, StockQuantity.Convert(line.Quantity, line.Unit, item.Unit, allowZero)));
        }
        return result;
    }

    private async Task<StockItem> GetItem(Guid id, CancellationToken ct) =>
        await db.StockItems.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("StockItem", id);
    private static string Name(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 150) throw new ArgumentException("Ingresá un nombre de hasta 150 caracteres.");
        return value.Trim();
    }
    private static void Positive(decimal quantity, StockUnit unit)
    {
        if (quantity <= 0) throw new ArgumentException("La cantidad debe ser mayor que cero.");
        StockQuantity.ValidateBase(quantity, unit);
    }
    private static void RequiredReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Indicá un motivo para que la diferencia quede documentada.");
    }
    private static StockItemDto MapItem(StockItem x) => new(x.Id, x.Name, x.Kind, x.Unit, x.ProductId, x.MinimumQuantity);
    private static RecipeDto MapRecipe(StockRecipe x) => new(x.Id, x.ItemId, x.Version, x.Yield, x.CreatedAtUtc,
        x.Lines.Select(l => new RecipeLineDto(l.ItemId, l.Quantity)).ToArray());
}
