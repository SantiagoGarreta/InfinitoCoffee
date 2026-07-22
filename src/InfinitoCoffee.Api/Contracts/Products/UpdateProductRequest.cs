using System.ComponentModel.DataAnnotations;
using InfinitoCoffee.Api.Contracts.Common;

namespace InfinitoCoffee.Api.Contracts.Products;

public sealed class UpdateProductRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Price { get; init; }

    [NotEmptyGuid(ErrorMessage = "CategoryId is required.")]
    public Guid CategoryId { get; init; }
}
