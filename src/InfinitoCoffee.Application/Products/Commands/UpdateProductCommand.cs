namespace InfinitoCoffee.Application.Products.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    Guid CategoryId,
    string Name,
    decimal Price,
    decimal Cost,
    string? Description);
