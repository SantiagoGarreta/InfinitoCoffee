using InfinitoCoffee.Api.Contracts;
using InfinitoCoffee.Api.Authorization;
using InfinitoCoffee.Api.Contracts.ProductCategories;
using InfinitoCoffee.Application.ProductCategories.Commands;
using InfinitoCoffee.Application.ProductCategories.Queries;
using InfinitoCoffee.Application.ProductCategories.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/product-categories")]
public sealed class ProductCategoriesController : ControllerBase
{
    private readonly ProductCategoryService _productCategoryService;

    public ProductCategoriesController(ProductCategoryService productCategoryService)
    {
        _productCategoryService = productCategoryService;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductCategoryResponse>> Create(
        [FromBody] CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _productCategoryService.CreateProductCategoryAsync(
            new CreateProductCategoryCommand(request.Name),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ApiContractMapper.MapProductCategory(category));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOrCashier)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ProductCategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _productCategoryService.GetProductCategoriesAsync(cancellationToken);
        return Ok(categories.Select(ApiContractMapper.MapProductCategory).ToArray());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOrCashier)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductCategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _productCategoryService.GetProductCategoryByIdAsync(
            new GetProductCategoryByIdQuery(id),
            cancellationToken);

        return Ok(ApiContractMapper.MapProductCategory(category));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductCategoryResponse>> Update(
        Guid id,
        [FromBody] UpdateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await _productCategoryService.UpdateProductCategoryAsync(
            new UpdateProductCategoryCommand(id, request.Name),
            cancellationToken);

        return Ok(ApiContractMapper.MapProductCategory(category));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductCategoryResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var category = await _productCategoryService.ActivateProductCategoryAsync(
            new ActivateProductCategoryCommand(id),
            cancellationToken);

        return Ok(ApiContractMapper.MapProductCategory(category));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
    [ProducesResponseType(typeof(ProductCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductCategoryResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var category = await _productCategoryService.DeactivateProductCategoryAsync(
            new DeactivateProductCategoryCommand(id),
            cancellationToken);

        return Ok(ApiContractMapper.MapProductCategory(category));
    }
}
