using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Application.Products.Commands;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Application.Products.Dtos;
using InfinitoCoffee.Application.Products.Queries;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Stock;

namespace InfinitoCoffee.Application.Products.Services;

public sealed class ProductService
{
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductStockRepository _productStockRepository;

    public ProductService(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        IProductStockRepository productStockRepository)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _productStockRepository = productStockRepository;
    }

    public async Task<ProductDto> CreateProductAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateMode(command.StockMode, command.MinimumQuantity, command.MinimumQuantityBranch2);

        var category = await GetActiveCategoryAsync(command.CategoryId, cancellationToken);
        var product = new Product(category.Id, command.Name, command.Price, command.Cost, command.Description);
        product.ChangeServiceOptions(command.CanConsumeOnSite, command.CanTakeAway, command.TakeAwayCost);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productStockRepository.AddForProductAsync(product, command.MinimumQuantity, command.StockMode, cancellationToken, command.MinimumQuantityBranch2);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return MapProduct(product, command.StockMode == ProductStockMode.Stocked ? command.MinimumQuantity : null, command.StockMode,
            command.StockMode == ProductStockMode.Stocked ? command.MinimumQuantityBranch2 ?? command.MinimumQuantity : null);
    }

    public async Task<ProductDto> GetProductByIdAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var product = await _productRepository.GetByIdAsync(query.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", query.ProductId);

        return MapProduct(product, await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken),
            await _productStockRepository.GetModeAsync(product.Id, cancellationToken),
            await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken, 2));
    }

    public async Task<IReadOnlyCollection<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var minimums = await _productStockRepository.GetMinimumQuantitiesAsync(
            products.Select(product => product.Id).ToArray(), cancellationToken);
        var minimumsBranch2 = await _productStockRepository.GetMinimumQuantitiesAsync(
            products.Select(product => product.Id).ToArray(), cancellationToken, 2);
        var modes = await _productStockRepository.GetModesAsync(
            products.Select(product => product.Id).ToArray(), cancellationToken);

        return products
            .OrderBy(product => product.Name, StringComparer.Ordinal)
            .ThenBy(product => product.Id)
            .Select(product => MapProduct(product,
                minimums.TryGetValue(product.Id, out var minimum) ? minimum : null,
                modes.TryGetValue(product.Id, out var mode) ? mode : ProductStockMode.Stocked,
                minimumsBranch2.TryGetValue(product.Id, out var minimumBranch2) ? minimumBranch2 : null))
            .ToArray();
    }

    public async Task<ProductDto> UpdateProductAsync(
        UpdateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.StockMode.HasValue)
            ValidateMode(command.StockMode.Value, command.MinimumQuantity, command.MinimumQuantityBranch2);
        else if (command.MinimumQuantity.HasValue)
            StockQuantity.ValidateBase(command.MinimumQuantity.Value, StockUnit.Unit);
        if (command.MinimumQuantityBranch2.HasValue)
            StockQuantity.ValidateBase(command.MinimumQuantityBranch2.Value, StockUnit.Unit);

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
        product.ChangeServiceOptions(command.CanConsumeOnSite, command.CanTakeAway, command.TakeAwayCost);

        var minimumQuantity = await _productStockRepository.UpdateForProductAsync(
            product, command.MinimumQuantity, command.StockMode, cancellationToken, command.MinimumQuantityBranch2);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return MapProduct(product, minimumQuantity,
            command.StockMode ?? await _productStockRepository.GetModeAsync(product.Id, cancellationToken),
            await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken, 2));
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

        return MapProduct(product, await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken),
            await _productStockRepository.GetModeAsync(product.Id, cancellationToken),
            await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken, 2));
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

        return MapProduct(product, await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken),
            await _productStockRepository.GetModeAsync(product.Id, cancellationToken),
            await _productStockRepository.GetMinimumQuantityAsync(product.Id, cancellationToken, 2));
    }

    public async Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("Product", productId);

        await _productStockRepository.ArchiveProductAsync(product, cancellationToken);
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

    private static void ValidateMode(ProductStockMode mode, decimal? minimumQuantity, decimal? minimumQuantityBranch2)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Elegí un modo de stock válido.");
        if (mode == ProductStockMode.Stocked)
        {
            StockQuantity.ValidateBase(minimumQuantity ?? 0m, StockUnit.Unit);
            if (minimumQuantityBranch2.HasValue) StockQuantity.ValidateBase(minimumQuantityBranch2.Value, StockUnit.Unit);
        }
        else if (minimumQuantity is > 0m || minimumQuantityBranch2 is > 0m)
            throw new ArgumentException("Un producto preparado al momento no tiene stock mínimo.");
    }

    private static ProductDto MapProduct(Product product, decimal? minimumQuantity, ProductStockMode mode, decimal? minimumQuantityBranch2)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Cost,
            product.CategoryId,
            product.IsActive,
            minimumQuantity,
            mode,
            product.CanConsumeOnSite,
            product.CanTakeAway,
            product.TakeAwayCost,
            minimumQuantityBranch2);
    }
}
