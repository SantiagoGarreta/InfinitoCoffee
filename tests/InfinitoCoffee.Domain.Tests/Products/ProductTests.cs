using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Domain.Tests.Products;

public class ProductTests
{
    [Fact]
    public void Create_ShouldInitializeActiveProduct()
    {
        var categoryId = Guid.NewGuid();

        var product = new Product(categoryId, " Flat White ", 6.50m, " Doble shot ");

        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal("Flat White", product.Name);
        Assert.Equal("Doble shot", product.Description);
        Assert.Equal(6.50m, product.Price);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void ChangePrice_ShouldRejectNegativeValues()
    {
        var product = new Product(Guid.NewGuid(), "Mocha", 7m);

        var action = () => product.ChangePrice(-1m);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void Deactivate_ShouldMarkProductAsInactive()
    {
        var product = new Product(Guid.NewGuid(), "Mocha", 7m);

        product.Deactivate();

        Assert.False(product.IsActive);
    }
}
