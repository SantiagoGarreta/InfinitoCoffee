using System.ComponentModel.DataAnnotations;

namespace InfinitoCoffee.Api.Contracts.ProductCategories;

public sealed class CreateProductCategoryRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;
}
