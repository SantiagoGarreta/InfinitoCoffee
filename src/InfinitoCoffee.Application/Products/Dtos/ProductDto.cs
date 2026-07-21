namespace InfinitoCoffee.Application.Products.Dtos;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    bool IsActive);
