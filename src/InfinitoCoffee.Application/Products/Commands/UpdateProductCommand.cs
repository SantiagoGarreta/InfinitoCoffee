using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Products.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    Guid CategoryId,
    string Name,
    decimal Price,
    decimal Cost,
    string? Description,
    decimal? MinimumQuantity = null,
    ProductStockMode? StockMode = null,
    bool CanConsumeOnSite = true,
    bool CanTakeAway = false,
    decimal TakeAwayCost = 0,
    decimal? MinimumQuantityBranch2 = null);
