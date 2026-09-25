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

public sealed class StockService(InfinitoCoffeeDbContext db, IDateTimeProvider clock) : IStockService
{
    public async Task<StockDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var items = await db.StockItems.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var balances = await db.StockBalances.AsNoTracking().ToListAsync(ct);
        var recipes = await db.StockRecipes.AsNoTracking().Include(x => x.Lines)
            .Where(x => !db.StockRecipes.Any(newer => newer.ItemId == x.ItemId && newer.Version > x.Version)).ToListAsync(ct);
        return new(items.Select(MapItem).ToArray(),
            balances.Select(x => new StockBalanceDto(x.ItemId, x.Location, x.Quantity, x.Revision)).ToArray(),
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
        foreach (var location in Enum.GetValues<StockLocation>())
            db.StockBalances.Add(new StockBalance { ItemId = item.Id, Location = location });
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

    public Task RecordReceiptAsync(ReceiptRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "Receipt", request, actorId, request.Notes, async op =>
        {
            PhysicalLocation(request.Location);
            foreach (var line in await NormalizeLines(request.Lines, false, ct))
                await StockLedger.PostAsync(db, op, line.Item, request.Location, line.Quantity, ct);
        }, ct);

    public Task RecordProductionAsync(ProductionRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "Production", request, actorId, request.Notes, async op =>
        {
            PhysicalLocation(request.Location);
            Positive(request.Quantity, StockUnit.Unit);
            StockQuantity.ValidateBase(request.DiscardedQuantity, StockUnit.Unit);
            if (request.DiscardedQuantity > request.Quantity) throw new ArgumentException("El descarte no puede superar la cantidad de la tanda.");
            if (request.DiscardedQuantity > 0) RequiredReason(request.Notes);
            var recipe = await db.StockRecipes.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == request.RecipeId, ct)
                ?? throw new NotFoundException("Recipe", request.RecipeId);
            // The client chooses an immutable recipe version; changing a recipe never rewrites past consumption.
            op.RecipeId = recipe.Id;
            foreach (var line in recipe.Lines)
            {
                var item = await GetItem(line.ItemId, ct);
                var consumed = line.Quantity * request.Quantity / recipe.Yield;
                Positive(consumed, item.Unit);
                await StockLedger.PostAsync(db, op, item, request.Location, -consumed, ct);
            }
            await StockLedger.PostAsync(db, op, await GetItem(recipe.ItemId, ct), request.Location, request.Quantity, ct);
            if (request.DiscardedQuantity > 0)
                await StockLedger.PostAsync(db, op, await GetItem(recipe.ItemId, ct), request.Location, -request.DiscardedQuantity, ct);
        }, ct);

    public Task SendTransferAsync(TransferRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "TransferSent", request, actorId, request.Notes, async op =>
        {
            PhysicalLocation(request.From);
            PhysicalLocation(request.To);
            if (request.From == request.To) throw new ArgumentException("Elegí un destino distinto del origen.");
            var transfer = new StockTransfer { Id = op.Id, From = request.From, To = request.To, SentAtUtc = op.CreatedAtUtc };
            foreach (var line in await NormalizeLines(request.Lines, false, ct))
            {
                await StockLedger.PostAsync(db, op, line.Item, request.From, -line.Quantity, ct);
                await StockLedger.PostAsync(db, op, line.Item, StockLocation.Transit, line.Quantity, ct);
                transfer.Lines.Add(new StockTransferLine { ItemId = line.Item.Id, Sent = line.Quantity });
            }
            db.StockTransfers.Add(transfer);
        }, ct);

    public Task ReceiveTransferAsync(Guid transferId, ReceiveTransferRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "TransferReceived", new { transferId, request }, actorId, request.Notes, async op =>
        {
            var transfer = await db.StockTransfers.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == transferId, ct)
                ?? throw new NotFoundException("Transfer", transferId);
            if (transfer.ReceivedAtUtc is not null) throw new ConflictException("El envío ya fue recibido.");
            var lines = await NormalizeLines(request.Lines, true, ct);
            if (lines.Count != transfer.Lines.Count || lines.Any(x => transfer.Lines.All(y => y.ItemId != x.Item.Id)))
                throw new ArgumentException("Confirmá la cantidad recibida de cada artículo del envío.");
            foreach (var line in lines)
            {
                var sent = transfer.Lines.Single(x => x.ItemId == line.Item.Id);
                if (line.Quantity > sent.Sent) throw new ArgumentException("La recepción no puede superar lo enviado. Registrá cualquier ingreso adicional por separado.");
                if (line.Quantity != sent.Sent) RequiredReason(request.Notes);
                await StockLedger.PostAsync(db, op, line.Item, StockLocation.Transit, -sent.Sent, ct);
                await StockLedger.PostAsync(db, op, line.Item, transfer.To, line.Quantity, ct);
                sent.Received = line.Quantity;
            }
            transfer.ReceivedAtUtc = op.CreatedAtUtc;
            transfer.ReceiptOperationId = op.Id;
            transfer.Revision = Guid.NewGuid();
        }, ct);

    public Task RecordWasteAsync(WasteRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "Waste", request, actorId, request.Notes, async op =>
        {
            PhysicalLocation(request.Location);
            RequiredReason(request.Notes);
            foreach (var line in await NormalizeLines(request.Lines, false, ct))
                await StockLedger.PostAsync(db, op, line.Item, request.Location, -line.Quantity, ct);
        }, ct);

    public Task RecordCountAsync(CountRequest request, Guid actorId, CancellationToken ct) =>
        Execute(request.OperationId, "Count", request, actorId, request.Notes, async op =>
        {
            PhysicalLocation(request.Location);
            RequiredReason(request.Notes);
            if (request.Lines is null || request.Lines.Any(x => x is null)) throw new ArgumentException("Ingresá los artículos del conteo.");
            var lines = await NormalizeLines(request.Lines.Select(x => new StockLineInput(x.ItemId, x.Quantity, x.Unit)).ToArray(), true, ct);
            foreach (var line in lines)
            {
                var balance = await db.StockBalances.SingleAsync(x => x.ItemId == line.Item.Id && x.Location == request.Location, ct);
                if (balance.Revision != request.Lines.Single(x => x.ItemId == line.Item.Id).ExpectedRevision)
                    throw new ConflictException("Hubo movimientos desde que abriste el conteo. Actualizá el stock y volvé a contar antes de confirmar.");
                await StockLedger.PostAsync(db, op, line.Item, request.Location, line.Quantity - balance.Quantity, ct);
            }
        }, ct);

    public async Task<IReadOnlyList<TransferDto>> GetTransfersAsync(bool pendingOnly, CancellationToken ct)
    {
        var query = db.StockTransfers.AsNoTracking().Include(x => x.Lines).AsQueryable();
        if (pendingOnly) query = query.Where(x => x.ReceivedAtUtc == null);
        // Pending transfers must never disappear behind a limit on recent completed transfers.
        query = query.Where(x => x.ReceivedAtUtc == null || db.StockTransfers.Where(t => t.ReceivedAtUtc != null)
            .OrderByDescending(t => t.SentAtUtc).Take(100).Select(t => t.Id).Contains(x.Id));
        var transfers = await query.OrderByDescending(x => x.SentAtUtc).ToListAsync(ct);
        return transfers.Select(x => new TransferDto(x.Id, x.From, x.To, x.SentAtUtc, x.ReceivedAtUtc,
            x.Lines.Select(l => new TransferLineDto(l.ItemId, l.Sent, l.Received)).ToArray())).ToArray();
    }

    public async Task<StockHistoryDto> GetHistoryAsync(int page, Guid? itemId, StockLocation? location, CancellationToken ct)
    {
        if (page < 0 || page > 100000) throw new ArgumentException("Página inválida.");
        if (location.HasValue && !Enum.IsDefined(location.Value)) throw new ArgumentException("Ubicación inválida.");
        var query = db.StockOperations.AsNoTracking().Include(x => x.Movements).AsQueryable();
        if (itemId.HasValue || location.HasValue)
            query = query.Where(x => x.Movements.Any(m => (!itemId.HasValue || m.ItemId == itemId)
                && (!location.HasValue || m.Location == location)));
        var operations = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip(page * 30).Take(31).ToListAsync(ct);
        var items = await db.StockItems.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var actors = operations.Where(x => x.ActorId.HasValue).Select(x => x.ActorId!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => actors.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var recipeIds = operations.Where(x => x.RecipeId.HasValue).Select(x => x.RecipeId!.Value).ToArray();
        var versions = await db.StockRecipes.AsNoTracking().Where(x => recipeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Version, ct);
        return new(operations.Take(30).Select(x => new OperationDto(x.Id, x.Type, x.CreatedAtUtc, x.ActorId,
            x.ActorId.HasValue ? users.GetValueOrDefault(x.ActorId.Value, "Administrador") : "Venta automática",
            x.Notes, x.RecipeId, x.RecipeId.HasValue ? versions[x.RecipeId.Value] : null, x.OrderId, x.Movements.Select(m => new MovementDto(m.ItemId, items[m.ItemId].Name,
                items[m.ItemId].Unit, m.Location, m.Before, m.Delta, m.After)).ToArray())).ToArray(), operations.Count > 30);
    }

    private async Task Execute(Guid operationId, string type, object request, Guid actorId, string? notes,
        Func<StockOperation, Task> action, CancellationToken ct)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Falta el identificador de la operación.");
        if ((notes?.Length ?? 0) > 1000) throw new ArgumentException("El motivo admite hasta 1000 caracteres.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
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
    private static void PhysicalLocation(StockLocation location)
    {
        if (location is not StockLocation.Factory and not StockLocation.Cafe) throw new ArgumentException("Elegí fábrica o cafetería.");
    }
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
