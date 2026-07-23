using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Domain.Tests.Products;

public class ProductCategoryTests
{
    [Fact]
    public void Create_ShouldInitializeActiveCategory()
    {
        var category = new ProductCategory(" Cafes frios ");

        Assert.Equal("Cafes frios", category.Name);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Rename_ShouldRequireName()
    {
        var category = new ProductCategory("Cafe");

        var action = () => category.Rename(" ");

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Deactivate_ShouldMarkCategoryAsInactive()
    {
        var category = new ProductCategory("Pasteleria");

        category.Deactivate();

        Assert.False(category.IsActive);
    }
}
