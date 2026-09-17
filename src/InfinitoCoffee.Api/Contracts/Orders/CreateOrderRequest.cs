using System.ComponentModel.DataAnnotations;

namespace InfinitoCoffee.Api.Contracts.Orders;

public sealed class CreateOrderRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateOrderItemRequest> Items { get; init; } = [];
}
