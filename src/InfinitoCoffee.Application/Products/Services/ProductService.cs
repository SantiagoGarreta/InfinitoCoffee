using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Application.Products.Commands;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Application.Products.Dtos;
using InfinitoCoffee.Application.Products.Queries;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.Products.Services;

public sealed class ProductService
{
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly IProductRepository _productRepository;

    public ProductService(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<ProductDto> CreateProductAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await GetActiveCategoryAsync(command.CategoryId, cancellationToken);
        var product = new Product(category.Id, command.Name, command.Price, command.Cost, command.Description);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    public async Task<ProductDto> GetProductByIdAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var product = await _productRepository.GetByIdAsync(query.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", query.ProductId);

        return MapProduct(product);
    }

    public async Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);

        return products
            .OrderBy(product => product.Name, StringComparer.Ordinal)
            .ThenBy(product => product.Id)
            .Select(MapProduct)
            .ToArray();
    }

    public async Task<ProductDto> UpdateProductAsync(
        UpdateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        if (product.CategoryId != command.CategoryId)
        {
            var category = await GetActiveCategoryAsync(command.CategoryId, cancellationToken);
            product.ChangeCategory(category.Id);
        }

        product.Rename(command.Name);
        product.ChangePrice(command.Price);
        product.ChangeCost(command.Cost);
        product.ChangeDescription(command.Description);

        await _productRepository.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    public async Task<ProductDto> ActivateProductAsync(
        ActivateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        product.Activate();
        await _productRepository.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    public async Task<ProductDto> DeactivateProductAsync(
        DeactivateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", command.ProductId);

        product.Deactivate();
        await _productRepository.SaveChangesAsync(cancellationToken);

        return MapProduct(product);
    }

    private async Task<ProductCategory> GetActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _productCategoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("ProductCategory", categoryId);

        if (!category.IsActive)
        {
            throw new ConflictException($"The category '{category.Name}' is inactive.");
        }

        return category;
    }

    private static ProductDto MapProduct(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Cost,
            product.CategoryId,
            product.IsActive);
    }
}
