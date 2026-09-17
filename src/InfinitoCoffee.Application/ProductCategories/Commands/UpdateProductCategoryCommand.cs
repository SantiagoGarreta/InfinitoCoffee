namespace InfinitoCoffee.Application.ProductCategories.Commands;

public sealed record UpdateProductCategoryCommand(
    Guid ProductCategoryId,
    string Name);
