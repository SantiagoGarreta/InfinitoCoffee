using InfinitoCoffee.Domain.Orders;

namespace InfinitoCoffee.Domain.Tests.Orders;

public class OrderItemTests
{
    [Fact]
    public void Create_ShouldTrimOptionalNotesAndCalculateLineTotal()
    {
        var item = new OrderItem(Guid.NewGuid(), " Cappuccino ", 4.25m, 1.50m, 3, " Sin canela ");

        Assert.Equal("Cappuccino", item.ProductNameSnapshot);
        Assert.Equal("Sin canela", item.Notes);
        Assert.Equal(12.75m, item.LineTotal);
        Assert.Equal(4.50m, item.CostTotal);
        Assert.Equal(8.25m, item.Profit);
    }

    [Fact]
    public void Create_ShouldRequirePositiveQuantity()
    {
        var action = () => new OrderItem(Guid.NewGuid(), "Latte", 5m, 0);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }
}
