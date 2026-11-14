using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Api.Contracts.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    decimal Cost,
    Guid? CategoryId,
    bool IsActive,
    decimal? MinimumQuantity,
    ProductStockMode StockMode,
    bool CanConsumeOnSite,
    bool CanTakeAway,
    decimal TakeAwayCost,
    decimal? MinimumQuantityBranch2 = null);
