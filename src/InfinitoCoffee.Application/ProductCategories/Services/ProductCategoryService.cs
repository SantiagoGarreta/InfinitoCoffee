using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.ProductCategories.Commands;
using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Application.ProductCategories.Dtos;
using InfinitoCoffee.Application.ProductCategories.Queries;
using InfinitoCoffee.Domain.Products;

namespace InfinitoCoffee.Application.ProductCategories.Services;

public sealed class ProductCategoryService
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    public ProductCategoryService(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<ProductCategoryDto> CreateProductCategoryAsync(
        CreateProductCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = new ProductCategory(command.Name);

        await _productCategoryRepository.AddAsync(category, cancellationToken);
        await _productCategoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<ProductCategoryDto> GetProductCategoryByIdAsync(
        GetProductCategoryByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var category = await _productCategoryRepository.GetByIdAsync(query.ProductCategoryId, cancellationToken)
            ?? throw new NotFoundException("ProductCategory", query.ProductCategoryId);

        return MapCategory(category);
    }

    public async Task<IReadOnlyCollection<ProductCategoryDto>> GetProductCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await _productCategoryRepository.GetAllAsync(cancellationToken);

        return categories
            .OrderBy(category => category.Name, StringComparer.Ordinal)
            .ThenBy(category => category.Id)
            .Select(MapCategory)
            .ToArray();
    }

    public async Task<ProductCategoryDto> UpdateProductCategoryAsync(
        UpdateProductCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _productCategoryRepository.GetByIdAsync(command.ProductCategoryId, cancellationToken)
            ?? throw new NotFoundException("ProductCategory", command.ProductCategoryId);

        category.Rename(command.Name);
        await _productCategoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<ProductCategoryDto> ActivateProductCategoryAsync(
        ActivateProductCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _productCategoryRepository.GetByIdAsync(command.ProductCategoryId, cancellationToken)
            ?? throw new NotFoundException("ProductCategory", command.ProductCategoryId);

        category.Activate();
        await _productCategoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<ProductCategoryDto> DeactivateProductCategoryAsync(
        DeactivateProductCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await _productCategoryRepository.GetByIdAsync(command.ProductCategoryId, cancellationToken)
            ?? throw new NotFoundException("ProductCategory", command.ProductCategoryId);

        category.Deactivate();
        await _productCategoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    private static ProductCategoryDto MapCategory(ProductCategory category)
    {
        return new ProductCategoryDto(
            category.Id,
            category.Name,
            category.IsActive);
    }
}
