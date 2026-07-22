using InfinitoCoffee.Domain.Common;
using InfinitoCoffee.Domain.Orders.Exceptions;

namespace InfinitoCoffee.Domain.Orders;

public class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
        OrderNumber = string.Empty;
    }

    public Order(
        string orderNumber,
        OrderSource source,
        DateTime createdAtUtc,
        IEnumerable<OrderItem> items,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new ArgumentException("Order number is required.", nameof(orderNumber));
        }

        ArgumentNullException.ThrowIfNull(items);

        var materializedItems = items.ToList();
        if (materializedItems.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        Id = Guid.NewGuid();
        OrderNumber = orderNumber.Trim();
        Source = source;
        Status = OrderStatus.Pending;
        CreatedAtUtc = UtcDateTime.Ensure(createdAtUtc, nameof(createdAtUtc));
        Notes = NormalizeOptional(notes);

        foreach (var item in materializedItems)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.AttachToOrder(Id);
            _items.Add(item);
        }
    }

    public Guid Id { get; private set; }

    public string OrderNumber { get; private set; }

    public OrderSource Source { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? ReadyAtUtc { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public bool IsActive => Status is not OrderStatus.Delivered and not OrderStatus.Cancelled;

    public decimal Total => _items.Sum(item => item.LineTotal);

    public void StartPreparing(DateTime startedAtUtc)
    {
        EnsureTransition(OrderStatus.Pending, OrderStatus.Preparing);

        StartedAtUtc = UtcDateTime.Ensure(startedAtUtc, nameof(startedAtUtc));
        Status = OrderStatus.Preparing;
    }

    public void MarkReady(DateTime readyAtUtc)
    {
        EnsureTransition(OrderStatus.Preparing, OrderStatus.Ready);

        ReadyAtUtc = UtcDateTime.Ensure(readyAtUtc, nameof(readyAtUtc));
        Status = OrderStatus.Ready;
    }

    public void Deliver(DateTime deliveredAtUtc)
    {
        EnsureTransition(OrderStatus.Ready, OrderStatus.Delivered);

        DeliveredAtUtc = UtcDateTime.Ensure(deliveredAtUtc, nameof(deliveredAtUtc));
        Status = OrderStatus.Delivered;
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status == OrderStatus.Delivered)
        {
            throw new InvalidOrderStateTransitionException(Status, OrderStatus.Cancelled);
        }

        if (Status == OrderStatus.Cancelled)
        {
            throw new InvalidOrderStateTransitionException(Status, OrderStatus.Cancelled);
        }

        CancelledAtUtc = UtcDateTime.Ensure(utcNow, nameof(utcNow));
        Status = OrderStatus.Cancelled;
    }

    public bool IsVisibleForPickup(DateTime utcNow, TimeSpan readyVisibilityDuration)
    {
        var validatedUtcNow = UtcDateTime.Ensure(utcNow, nameof(utcNow));

        if (readyVisibilityDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(readyVisibilityDuration), "Visibility duration cannot be negative.");
        }

        if (Status != OrderStatus.Ready || ReadyAtUtc is null)
        {
            return false;
        }

        return validatedUtcNow - ReadyAtUtc.Value <= readyVisibilityDuration;
    }

    private void EnsureTransition(OrderStatus expectedCurrentStatus, OrderStatus targetStatus)
    {
        if (Status != expectedCurrentStatus)
        {
            throw new InvalidOrderStateTransitionException(Status, targetStatus);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
