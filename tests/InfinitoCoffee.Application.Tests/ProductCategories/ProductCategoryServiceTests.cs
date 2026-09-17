using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.ProductCategories.Commands;
using InfinitoCoffee.Application.ProductCategories.Queries;
using InfinitoCoffee.Application.ProductCategories.Services;
using InfinitoCoffee.Application.Tests.Fakes;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.ProductCategories;

public class ProductCategoryServiceTests
{
    [Fact]
    public async Task CreateProductCategoryAsync_ValidData_CreatesCategory()
    {
        var repository = new FakeProductCategoryRepository();
        var service = new ProductCategoryService(repository);

        var result = await service.CreateProductCategoryAsync(new CreateProductCategoryCommand("Bakery"));

        Assert.Equal("Bakery", result.Name);
        Assert.True(result.IsActive);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateProductCategoryAsync_ExistingCategory_UpdatesCategory()
    {
        var repository = new FakeProductCategoryRepository();
        var category = new ProductCategory("Coffee");
        repository.Seed(category);
        var service = new ProductCategoryService(repository);

        var result = await service.UpdateProductCategoryAsync(new UpdateProductCategoryCommand(category.Id, "Specialty Coffee"));

        Assert.Equal("Specialty Coffee", result.Name);
    }

    [Fact]
    public async Task ActivateProductCategoryAsync_ExistingCategory_ActivatesCategory()
    {
        var repository = new FakeProductCategoryRepository();
        var category = new ProductCategory("Tea");
        category.Deactivate();
        repository.Seed(category);
        var service = new ProductCategoryService(repository);

        var result = await service.ActivateProductCategoryAsync(new ActivateProductCategoryCommand(category.Id));

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task DeactivateProductCategoryAsync_ExistingCategory_DeactivatesCategory()
    {
        var repository = new FakeProductCategoryRepository();
        var category = new ProductCategory("Tea");
        repository.Seed(category);
        var service = new ProductCategoryService(repository);

        var result = await service.DeactivateProductCategoryAsync(new DeactivateProductCategoryCommand(category.Id));

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task GetProductCategoriesAsync_MultipleCategories_ReturnsCategoriesOrderedByName()
    {
        var repository = new FakeProductCategoryRepository();
        repository.Seed(
            new ProductCategory("Bakery"),
            new ProductCategory("Coffee"));
        var service = new ProductCategoryService(repository);

        var result = await service.GetProductCategoriesAsync();

        Assert.Collection(
            result,
            first => Assert.Equal("Bakery", first.Name),
            second => Assert.Equal("Coffee", second.Name));
    }

    [Fact]
    public async Task GetProductCategoryByIdAsync_MissingCategory_ThrowsNotFoundException()
    {
        var service = new ProductCategoryService(new FakeProductCategoryRepository());

        var action = () => service.GetProductCategoryByIdAsync(new GetProductCategoryByIdQuery(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotFoundException>(action);
    }
}
