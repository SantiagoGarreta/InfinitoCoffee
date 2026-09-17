using InfinitoCoffee.Domain.Orders.Exceptions;

namespace InfinitoCoffee.Domain.Orders;

public class OrderItem
{
    private OrderItem()
    {
        ProductNameSnapshot = string.Empty;
    }

    public OrderItem(Guid productId, string productNameSnapshot, decimal unitPriceSnapshot, int quantity, string? notes = null)
        : this(productId, productNameSnapshot, unitPriceSnapshot, 0m, quantity, notes)
    {
    }

    public OrderItem(
        Guid productId,
        string productNameSnapshot,
        decimal unitPriceSnapshot,
        decimal unitCostSnapshot,
        int quantity,
        string? notes = null)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(productNameSnapshot))
        {
            throw new ArgumentException("Product name snapshot is required.", nameof(productNameSnapshot));
        }

        if (unitPriceSnapshot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPriceSnapshot), "Unit price snapshot cannot be negative.");
        }

        if (unitCostSnapshot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCostSnapshot), "Unit cost snapshot cannot be negative.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot.Trim();
        UnitPriceSnapshot = unitPriceSnapshot;
        UnitCostSnapshot = unitCostSnapshot;
        Quantity = quantity;
        Notes = NormalizeOptional(notes);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductNameSnapshot { get; private set; }

    public decimal UnitPriceSnapshot { get; private set; }

    public decimal UnitCostSnapshot { get; private set; }

    public int Quantity { get; private set; }

    public string? Notes { get; private set; }

    public decimal LineTotal => UnitPriceSnapshot * Quantity;

    public decimal CostTotal => UnitCostSnapshot * Quantity;

    public decimal Profit => LineTotal - CostTotal;

    internal void AttachToOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        }

        if (OrderId != Guid.Empty && OrderId != orderId)
        {
            throw new DomainException("The order item is already attached to another order.");
        }

        OrderId = orderId;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
