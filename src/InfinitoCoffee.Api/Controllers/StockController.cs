using System.Security.Claims;
using InfinitoCoffee.Api.Authorization;
using InfinitoCoffee.Application.Stock;
using InfinitoCoffee.Domain.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
[Route("api/stock")]
public sealed class StockController(IStockService stock) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public Task<StockDashboardDto> Dashboard(CancellationToken ct) => stock.GetDashboardAsync(ct);

    [HttpPost("items")]
    public async Task<ActionResult<StockItemDto>> CreateItem(StockItemRequest request, CancellationToken ct)
        => Ok(await stock.CreateItemAsync(request, ct));

    [HttpPut("items/{id:guid}")]
    public Task<StockItemDto> UpdateItem(Guid id, StockItemUpdate request, CancellationToken ct)
        => stock.UpdateItemAsync(id, request, ct);

    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        await stock.DeleteItemAsync(id, ct);
        return NoContent();
    }

    [HttpPost("recipes")]
    public Task<RecipeDto> SaveRecipe(RecipeRequest request, CancellationToken ct)
        => stock.SaveRecipeAsync(request, ActorId, ct);

    [HttpDelete("recipes/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ArchiveRecipe(Guid itemId, CancellationToken ct)
    {
        await stock.ArchiveRecipeAsync(itemId, ct);
        return NoContent();
    }

    [HttpPost("adjustments")]
    public async Task<IActionResult> Adjustment(StockAdjustmentRequest request, CancellationToken ct)
    {
        await stock.RecordAdjustmentAsync(request, ActorId, ct);
        return NoContent();
    }

    [HttpGet("history")]
    public Task<StockHistoryDto> History([FromQuery] int page, [FromQuery] Guid? itemId,
        CancellationToken ct) => stock.GetHistoryAsync(page, itemId, ct);
}
