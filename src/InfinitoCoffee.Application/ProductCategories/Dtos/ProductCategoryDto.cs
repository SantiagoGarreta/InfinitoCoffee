namespace InfinitoCoffee.Application.ProductCategories.Dtos;

public sealed record ProductCategoryDto(
    Guid Id,
    string Name,
    bool IsActive);
