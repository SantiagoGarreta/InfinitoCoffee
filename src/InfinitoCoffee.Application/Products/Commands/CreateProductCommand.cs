namespace InfinitoCoffee.Application.Products.Commands;

public sealed record CreateProductCommand(
    Guid CategoryId,
    string Name,
    decimal Price,
    string? Description);
