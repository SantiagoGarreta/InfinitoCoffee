using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Application.Products.Commands;
using InfinitoCoffee.Application.Products.Queries;
using InfinitoCoffee.Application.Products.Services;
using InfinitoCoffee.Application.Tests.Fakes;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Tests.Products;

public class ProductServiceTests
{
    [Fact]
    public async Task CreateProductAsync_ValidCategory_CreatesProduct()
    {
        var categoryRepository = new FakeProductCategoryRepository();
        var category = new ProductCategory("Coffee");
        categoryRepository.Seed(category);
        var productRepository = new FakeProductRepository();
        var service = CreateProductService(productRepository, categoryRepository);

        var result = await service.CreateProductAsync(new CreateProductCommand(category.Id, "Flat White", 6.50m, "Double shot"));

        Assert.Equal("Flat White", result.Name);
        Assert.Equal(category.Id, result.CategoryId);
        Assert.Equal(1, productRepository.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateProductAsync_MissingCategory_ThrowsNotFoundException()
    {
        var service = CreateProductService(new FakeProductRepository(), new FakeProductCategoryRepository());

        var action = () => service.CreateProductAsync(new CreateProductCommand(Guid.NewGuid(), "Latte", 7m, null));

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    [Fact]
    public async Task CreateProductAsync_InactiveCategory_ThrowsConflictException()
    {
        var categoryRepository = new FakeProductCategoryRepository();
        var category = new ProductCategory("Coffee");
        category.Deactivate();
        categoryRepository.Seed(category);
        var service = CreateProductService(new FakeProductRepository(), categoryRepository);

        var action = () => service.CreateProductAsync(new CreateProductCommand(category.Id, "Latte", 7m, null));

        await Assert.ThrowsAsync<ConflictException>(action);
    }

    [Fact]
    public async Task UpdateProductAsync_ValidData_UpdatesProduct()
    {
        var categoryRepository = new FakeProductCategoryRepository();
        var currentCategory = new ProductCategory("Coffee");
        var newCategory = new ProductCategory("Signature");
        categoryRepository.Seed(currentCategory, newCategory);
        var productRepository = new FakeProductRepository();
        var product = new Product(currentCategory.Id, "Latte", 7m, "Whole milk");
        productRepository.Seed(product);
        var service = CreateProductService(productRepository, categoryRepository);

        var result = await service.UpdateProductAsync(new UpdateProductCommand(
            product.Id,
            newCategory.Id,
            "Iced Latte",
            7.75m,
            "Oat milk"));

        Assert.Equal("Iced Latte", result.Name);
        Assert.Equal(newCategory.Id, result.CategoryId);
        Assert.Equal(7.75m, result.Price);
        Assert.Equal("Oat milk", result.Description);
    }

    [Fact]
    public async Task ActivateProductAsync_ExistingProduct_ActivatesProduct()
    {
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Mocha", 8m);
        product.Deactivate();
        productRepository.Seed(product);
        var service = CreateProductService(productRepository, new FakeProductCategoryRepository());

        var result = await service.ActivateProductAsync(new ActivateProductCommand(product.Id));

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task DeactivateProductAsync_ExistingProduct_DeactivatesProduct()
    {
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Mocha", 8m);
        productRepository.Seed(product);
        var service = CreateProductService(productRepository, new FakeProductCategoryRepository());

        var result = await service.DeactivateProductAsync(new DeactivateProductCommand(product.Id));

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DeleteProductAsync_ExistingProduct_RemovesProduct()
    {
        var productRepository = new FakeProductRepository();
        var product = new Product(Guid.NewGuid(), "Mocha", 8m);
        productRepository.Seed(product);
        var service = CreateProductService(productRepository, new FakeProductCategoryRepository());

        await service.DeleteProductAsync(new DeleteProductCommand(product.Id));

        var remainingProducts = await productRepository.GetAllAsync();
        Assert.Empty(remainingProducts);
        Assert.Equal(1, productRepository.SaveChangesCalls);
    }

    [Fact]
    public async Task GetProductsAsync_MultipleProducts_ReturnsProductsOrderedByName()
    {
        var productRepository = new FakeProductRepository();
        productRepository.Seed(
            new Product(Guid.NewGuid(), "Mocha", 8m),
            new Product(Guid.NewGuid(), "Americano", 5m));
        var service = CreateProductService(productRepository, new FakeProductCategoryRepository());

        var result = await service.GetProductsAsync();

        Assert.Collection(
            result,
            first => Assert.Equal("Americano", first.Name),
            second => Assert.Equal("Mocha", second.Name));
    }

    [Fact]
    public async Task GetProductByIdAsync_MissingProduct_ThrowsNotFoundException()
    {
        var service = CreateProductService(new FakeProductRepository(), new FakeProductCategoryRepository());

        var action = () => service.GetProductByIdAsync(new GetProductByIdQuery(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotFoundException>(action);
    }

    private static ProductService CreateProductService(
        FakeProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository)
    {
        return new ProductService(productRepository, productCategoryRepository);
    }
}
