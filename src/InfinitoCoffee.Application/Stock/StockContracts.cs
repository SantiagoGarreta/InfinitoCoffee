using InfinitoCoffee.Domain.Stock;

namespace InfinitoCoffee.Application.Stock;

public sealed record StockItemRequest(string Name, StockItemKind Kind, StockUnit Unit, Guid? ProductId, decimal MinimumQuantity = 0);
public sealed record StockItemUpdate(string Name, decimal MinimumQuantity);
public sealed record StockLineInput(Guid ItemId, decimal Quantity, string Unit);
public sealed record RecipeRequest(Guid ItemId, decimal Yield, IReadOnlyList<StockLineInput> Lines);
public sealed record ReceiptRequest(Guid OperationId, StockLocation Location, string? Notes, IReadOnlyList<StockLineInput> Lines);
public sealed record ProductionRequest(Guid OperationId, Guid RecipeId, decimal Quantity, StockLocation Location, string? Notes, decimal DiscardedQuantity = 0);
public sealed record TransferRequest(Guid OperationId, StockLocation From, StockLocation To, string? Notes, IReadOnlyList<StockLineInput> Lines);
public sealed record ReceiveTransferRequest(Guid OperationId, string? Notes, IReadOnlyList<StockLineInput> Lines);
public sealed record WasteRequest(Guid OperationId, StockLocation Location, string Notes, IReadOnlyList<StockLineInput> Lines);
public sealed record CountLineInput(Guid ItemId, decimal Quantity, string Unit, Guid ExpectedRevision);
public sealed record CountRequest(Guid OperationId, StockLocation Location, string Notes, IReadOnlyList<CountLineInput> Lines);

public sealed record StockItemDto(Guid Id, string Name, StockItemKind Kind, StockUnit Unit, Guid? ProductId, decimal MinimumQuantity);
public sealed record StockBalanceDto(Guid ItemId, StockLocation Location, decimal Quantity, Guid Revision);
public sealed record RecipeLineDto(Guid ItemId, decimal Quantity);
public sealed record RecipeDto(Guid Id, Guid ItemId, int Version, decimal Yield, DateTime CreatedAtUtc, IReadOnlyList<RecipeLineDto> Lines);
public sealed record StockDashboardDto(IReadOnlyList<StockItemDto> Items, IReadOnlyList<StockBalanceDto> Balances, IReadOnlyList<RecipeDto> Recipes);
public sealed record TransferLineDto(Guid ItemId, decimal Sent, decimal? Received);
public sealed record TransferDto(Guid Id, StockLocation From, StockLocation To, DateTime SentAtUtc, DateTime? ReceivedAtUtc, IReadOnlyList<TransferLineDto> Lines);
public sealed record MovementDto(Guid ItemId, string ItemName, StockUnit Unit, StockLocation Location, decimal Before, decimal Delta, decimal After);
public sealed record OperationDto(Guid Id, string Type, DateTime CreatedAtUtc, Guid? ActorId, string ActorName, string Notes, Guid? RecipeId, int? RecipeVersion, Guid? OrderId, IReadOnlyList<MovementDto> Movements);
public sealed record StockHistoryDto(IReadOnlyList<OperationDto> Items, bool HasMore);

public interface IStockService
{
    Task<StockDashboardDto> GetDashboardAsync(CancellationToken ct);
    Task<StockItemDto> CreateItemAsync(StockItemRequest request, CancellationToken ct);
    Task<StockItemDto> UpdateItemAsync(Guid id, StockItemUpdate request, CancellationToken ct);
    Task<RecipeDto> SaveRecipeAsync(RecipeRequest request, Guid actorId, CancellationToken ct);
    Task RecordReceiptAsync(ReceiptRequest request, Guid actorId, CancellationToken ct);
    Task RecordProductionAsync(ProductionRequest request, Guid actorId, CancellationToken ct);
    Task SendTransferAsync(TransferRequest request, Guid actorId, CancellationToken ct);
    Task ReceiveTransferAsync(Guid transferId, ReceiveTransferRequest request, Guid actorId, CancellationToken ct);
    Task RecordWasteAsync(WasteRequest request, Guid actorId, CancellationToken ct);
    Task RecordCountAsync(CountRequest request, Guid actorId, CancellationToken ct);
    Task<IReadOnlyList<TransferDto>> GetTransfersAsync(bool pendingOnly, CancellationToken ct);
    Task<StockHistoryDto> GetHistoryAsync(int page, Guid? itemId, StockLocation? location, CancellationToken ct);
}
