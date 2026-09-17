using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Orders.Exceptions;

namespace InfinitoCoffee.Domain.Tests.Orders;

public class OrderTests
{
    [Fact]
    public void Create_ShouldInitializePendingOrder()
    {
        var createdAtUtc = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        var item = CreateItem(quantity: 2, price: 5.50m);

        var order = new Order("260721-0001", createdAtUtc, [item], " Sin azucar ");

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(createdAtUtc, order.CreatedAtUtc);
        Assert.Null(order.StartedAtUtc);
        Assert.Null(order.ReadyAtUtc);
        Assert.Null(order.DeliveredAtUtc);
        Assert.Null(order.CancelledAtUtc);
        Assert.Equal("Sin azucar", order.Notes);
        Assert.True(order.IsActive);
        Assert.Single(order.Items);
        Assert.Equal(order.Id, order.Items.Single().OrderId);
        Assert.Equal(11.00m, order.Total);
    }

    [Fact]
    public void Create_ShouldRequireAtLeastOneItem()
    {
        var createdAtUtc = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

        var action = () => new Order("260721-0001", createdAtUtc, []);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void StartPreparing_ShouldChangeStatusAndSetTimestamp()
    {
        var order = CreatePendingOrder();
        var startedAtUtc = new DateTime(2026, 7, 21, 12, 3, 0, DateTimeKind.Utc);

        order.StartPreparing(startedAtUtc);

        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Equal(startedAtUtc, order.StartedAtUtc);
        Assert.True(order.IsActive);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void IsActive_ShouldReflectStatus(OrderStatus status, bool expected)
    {
        var order = CreateOrderInStatus(status);

        Assert.Equal(expected, order.IsActive);
    }

    [Fact]
    public void MarkReady_ShouldRequirePreparingStatus()
    {
        var order = CreatePendingOrder();
        var readyAtUtc = new DateTime(2026, 7, 21, 12, 5, 0, DateTimeKind.Utc);

        var action = () => order.MarkReady(readyAtUtc);

        var exception = Assert.Throws<InvalidOrderStateTransitionException>(action);
        Assert.Equal(OrderStatus.Pending, exception.CurrentStatus);
        Assert.Equal(OrderStatus.Ready, exception.TargetStatus);
    }

    [Fact]
    public void Lifecycle_ShouldAllowPendingToPreparingToReadyToDelivered()
    {
        var order = CreatePendingOrder();
        var startedAtUtc = new DateTime(2026, 7, 21, 12, 2, 0, DateTimeKind.Utc);
        var readyAtUtc = new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc);
        var deliveredAtUtc = new DateTime(2026, 7, 21, 12, 10, 0, DateTimeKind.Utc);

        order.StartPreparing(startedAtUtc);
        order.MarkReady(readyAtUtc);
        order.Deliver(deliveredAtUtc);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(startedAtUtc, order.StartedAtUtc);
        Assert.Equal(readyAtUtc, order.ReadyAtUtc);
        Assert.Equal(deliveredAtUtc, order.DeliveredAtUtc);
        Assert.False(order.IsActive);
    }

    [Fact]
    public void Cancel_ShouldAllowAnyNonDeliveredOrder()
    {
        var order = CreatePendingOrder();
        var cancelledAtUtc = new DateTime(2026, 7, 21, 12, 4, 0, DateTimeKind.Utc);

        order.Cancel(cancelledAtUtc);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(cancelledAtUtc, order.CancelledAtUtc);
        Assert.False(order.IsActive);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_ShouldNotAllowRepeatedOrInvalidCancellation(OrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);
        if (initialStatus != OrderStatus.Cancelled)
        {
            order.Cancel(new DateTime(2026, 7, 21, 12, 7, 0, DateTimeKind.Utc));
        }

        var action = () => order.Cancel(new DateTime(2026, 7, 21, 12, 8, 0, DateTimeKind.Utc));

        Assert.Throws<InvalidOrderStateTransitionException>(action);
    }

    [Fact]
    public void Cancel_ShouldNotAllowDeliveredOrder()
    {
        var order = CreateOrderInStatus(OrderStatus.Delivered);
        var previousCancelledAtUtc = order.CancelledAtUtc;

        var action = () => order.Cancel(new DateTime(2026, 7, 21, 12, 11, 0, DateTimeKind.Utc));

        Assert.Throws<InvalidOrderStateTransitionException>(action);
        Assert.Equal(previousCancelledAtUtc, order.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_ShouldRequireUtcTimestamp()
    {
        var order = CreatePendingOrder();
        var localTime = new DateTime(2026, 7, 21, 12, 4, 0, DateTimeKind.Local);

        var action = () => order.Cancel(localTime);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("utcNow", exception.ParamName);
    }

    [Fact]
    public void Cancel_InvalidTransition_ShouldNotModifyCancelledAtUtc()
    {
        var order = CreateDeliveredOrder();
        var previousCancelledAtUtc = order.CancelledAtUtc;

        var action = () => order.Cancel(new DateTime(2026, 7, 21, 12, 15, 0, DateTimeKind.Utc));

        Assert.Throws<InvalidOrderStateTransitionException>(action);
        Assert.Equal(previousCancelledAtUtc, order.CancelledAtUtc);
    }

    [Fact]
    public void Total_ShouldReturnSumForSingleItem()
    {
        var order = CreatePendingOrder();

        Assert.Equal(7.25m, order.Total);
    }

    [Fact]
    public void Total_ShouldReturnSumForMultipleItems()
    {
        var order = new Order(
            "260721-0002",
            new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            [
                CreateItem(quantity: 2, price: 4.50m),
                new OrderItem(Guid.NewGuid(), "Cookie", 3m, 1)
            ]);

        Assert.Equal(12m, order.Total);
    }

    [Fact]
    public void IsVisibleForPickup_ShouldReturnFalseBeforeOrderIsReady()
    {
        var order = CreatePendingOrder();

        var visible = order.IsVisibleForPickup(
            new DateTime(2026, 7, 21, 12, 10, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(15));

        Assert.False(visible);
    }

    [Fact]
    public void IsVisibleForPickup_ShouldReturnTrueAtBeginningOfVisibilityWindow()
    {
        var readyAtUtc = new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc);
        var order = CreateOrderInStatus(OrderStatus.Ready, readyAtUtc: readyAtUtc);
        var currentUtc = readyAtUtc;

        var visible = order.IsVisibleForPickup(currentUtc, TimeSpan.FromMinutes(15));

        Assert.True(visible);
    }

    [Fact]
    public void IsVisibleForPickup_ShouldReturnTrueDuringVisibilityWindow()
    {
        var readyAtUtc = new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc);
        var order = CreateOrderInStatus(OrderStatus.Ready, readyAtUtc: readyAtUtc);
        var currentUtc = readyAtUtc.AddMinutes(10);

        var visible = order.IsVisibleForPickup(currentUtc, TimeSpan.FromMinutes(15));

        Assert.True(visible);
    }

    [Fact]
    public void IsVisibleForPickup_ShouldReturnFalseAfterVisibilityWindow()
    {
        var readyAtUtc = new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc);
        var order = CreateOrderInStatus(OrderStatus.Ready, readyAtUtc: readyAtUtc);
        var currentUtc = readyAtUtc.AddMinutes(16);

        var visible = order.IsVisibleForPickup(currentUtc, TimeSpan.FromMinutes(15));

        Assert.False(visible);
    }

    [Fact]
    public void IsVisibleForPickup_ShouldRequireUtcNow()
    {
        var order = CreateOrderInStatus(
            OrderStatus.Ready,
            readyAtUtc: new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc));
        var localNow = new DateTime(2026, 7, 21, 12, 10, 0, DateTimeKind.Local);

        var action = () =>
        {
            order.IsVisibleForPickup(localNow, TimeSpan.FromMinutes(15));
        };

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("utcNow", exception.ParamName);
    }

    [Fact]
    public void IsVisibleForPickup_ShouldRejectNegativeVisibilityDuration()
    {
        var order = CreateOrderInStatus(
            OrderStatus.Ready,
            readyAtUtc: new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc));

        var action = () =>
        {
            order.IsVisibleForPickup(
                new DateTime(2026, 7, 21, 12, 10, 0, DateTimeKind.Utc),
                TimeSpan.FromMinutes(-1));
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("readyVisibilityDuration", exception.ParamName);
    }

    [Fact]
    public void StartPreparing_ShouldRequireUtcTimestamp()
    {
        var order = CreatePendingOrder();
        var localTime = new DateTime(2026, 7, 21, 12, 3, 0, DateTimeKind.Local);

        var action = () => order.StartPreparing(localTime);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("startedAtUtc", exception.ParamName);
    }

    private static Order CreatePendingOrder()
    {
        return new Order(
            "260721-0001",
            new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc),
            [CreateItem()]);
    }

    private static Order CreateOrderInStatus(OrderStatus targetStatus, DateTime? readyAtUtc = null)
    {
        var order = CreatePendingOrder();

        if (targetStatus == OrderStatus.Pending)
        {
            return order;
        }

        order.StartPreparing(new DateTime(2026, 7, 21, 12, 2, 0, DateTimeKind.Utc));

        if (targetStatus == OrderStatus.Preparing)
        {
            return order;
        }

        order.MarkReady(readyAtUtc ?? new DateTime(2026, 7, 21, 12, 6, 0, DateTimeKind.Utc));

        if (targetStatus == OrderStatus.Ready)
        {
            return order;
        }

        if (targetStatus == OrderStatus.Delivered)
        {
            order.Deliver(new DateTime(2026, 7, 21, 12, 10, 0, DateTimeKind.Utc));
            return order;
        }

        order.Cancel(new DateTime(2026, 7, 21, 12, 8, 0, DateTimeKind.Utc));
        return order;
    }

    private static Order CreateDeliveredOrder()
    {
        return CreateOrderInStatus(OrderStatus.Delivered);
    }

    private static OrderItem CreateItem(int quantity = 1, decimal price = 7.25m)
    {
        return new OrderItem(Guid.NewGuid(), "Latte", price, quantity, " Extra hot ");
    }
}
