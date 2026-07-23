using InfinitoCoffee.Api.Contracts;
using InfinitoCoffee.Api.Contracts.Products;
using InfinitoCoffee.Application.Products.Commands;
using InfinitoCoffee.Application.Products.Queries;
using InfinitoCoffee.Application.Products.Services;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(
            new CreateProductCommand(request.CategoryId, request.Name, request.Price, request.Description),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ApiContractMapper.MapProduct(product));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ProductResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productService.GetProductsAsync(cancellationToken);
        return Ok(products.Select(ApiContractMapper.MapProduct).ToArray());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(new GetProductByIdQuery(id), cancellationToken);
        return Ok(ApiContractMapper.MapProduct(product));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.UpdateProductAsync(
            new UpdateProductCommand(id, request.CategoryId, request.Name, request.Price, request.Description),
            cancellationToken);

        return Ok(ApiContractMapper.MapProduct(product));
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.ActivateProductAsync(new ActivateProductCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapProduct(product));
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.DeactivateProductAsync(new DeactivateProductCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapProduct(product));
    }
}
