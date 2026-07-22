namespace InfinitoCoffee.Api.Contracts.ProductCategories;

public sealed record ProductCategoryResponse(
    Guid Id,
    string Name,
    bool IsActive);
