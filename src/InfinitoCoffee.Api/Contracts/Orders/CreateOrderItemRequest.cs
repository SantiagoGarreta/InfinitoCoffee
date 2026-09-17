using System.ComponentModel.DataAnnotations;
using InfinitoCoffee.Api.Contracts.Common;

namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed class CreateOrderItemRequest
{
    [NotEmptyGuid(ErrorMessage = "ProductId is required.")]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public int Quantity { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
