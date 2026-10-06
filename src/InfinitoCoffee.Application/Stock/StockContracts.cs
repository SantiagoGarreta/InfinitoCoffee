using InfinitoCoffee.Domain.Stock;

namespace InfinitoCoffee.Application.Stock;

public sealed record StockItemRequest(string Name, StockItemKind Kind, StockUnit Unit, Guid? ProductId, decimal MinimumQuantity = 0);
public sealed record StockItemUpdate(string Name, decimal MinimumQuantity);
public sealed record StockLineInput(Guid ItemId, decimal Quantity, string Unit);
public sealed record RecipeRequest(Guid ItemId, decimal Yield, IReadOnlyList<StockLineInput> Lines);
public sealed record StockAdjustmentRequest(Guid OperationId, string Notes, IReadOnlyList<StockAdjustmentLine> Lines, bool ConsumeIngredients = true);
public sealed record StockAdjustmentLine(Guid ItemId, decimal Delta, string Unit);

public sealed record StockItemDto(Guid Id, string Name, StockItemKind Kind, StockUnit Unit, Guid? ProductId, decimal MinimumQuantity);
public sealed record StockBalanceDto(Guid ItemId, StockLocation Location, decimal Quantity, Guid Revision, int BranchId = 0);
public sealed record RecipeLineDto(Guid ItemId, decimal Quantity);
public sealed record RecipeDto(Guid Id, Guid ItemId, int Version, decimal Yield, DateTime CreatedAtUtc, IReadOnlyList<RecipeLineDto> Lines);
public sealed record StockDashboardDto(IReadOnlyList<StockItemDto> Items, IReadOnlyList<StockBalanceDto> Balances, IReadOnlyList<RecipeDto> Recipes);
public sealed record MovementDto(Guid ItemId, string ItemName, StockUnit Unit, StockLocation Location, decimal Before, decimal Delta, decimal After, int BranchId = 0);
public sealed record OperationDto(Guid Id, string Type, DateTime CreatedAtUtc, Guid? ActorId, string ActorName, string Notes, Guid? RecipeId, int? RecipeVersion, Guid? OrderId, IReadOnlyList<MovementDto> Movements, int BranchId = 1);
public sealed record StockHistoryDto(IReadOnlyList<OperationDto> Items, bool HasMore);

public interface IStockService
{
    Task<StockDashboardDto> GetDashboardAsync(CancellationToken ct);
    Task<StockItemDto> CreateItemAsync(StockItemRequest request, CancellationToken ct);
    Task<StockItemDto> UpdateItemAsync(Guid id, StockItemUpdate request, CancellationToken ct);
    Task<RecipeDto> SaveRecipeAsync(RecipeRequest request, Guid actorId, CancellationToken ct);
    Task RecordAdjustmentAsync(StockAdjustmentRequest request, Guid actorId, CancellationToken ct);
    Task<StockHistoryDto> GetHistoryAsync(int page, Guid? itemId, CancellationToken ct);
}
