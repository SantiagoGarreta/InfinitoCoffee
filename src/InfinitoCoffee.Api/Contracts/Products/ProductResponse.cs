namespace InfinitoCoffee.Api.Contracts.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    bool IsActive);
