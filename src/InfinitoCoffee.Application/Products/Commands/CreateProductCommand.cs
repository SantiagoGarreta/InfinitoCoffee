using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Products.Commands;

public sealed record CreateProductCommand(
    Guid CategoryId,
    string Name,
    decimal Price,
    decimal Cost,
    string? Description,
    decimal MinimumQuantity = 0,
    ProductStockMode StockMode = ProductStockMode.Stocked);
