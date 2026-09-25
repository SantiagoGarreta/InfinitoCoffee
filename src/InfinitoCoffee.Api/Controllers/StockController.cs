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

    [HttpPost("recipes")]
    public Task<RecipeDto> SaveRecipe(RecipeRequest request, CancellationToken ct)
        => stock.SaveRecipeAsync(request, ActorId, ct);

    [HttpPost("receipts")]
    public async Task<IActionResult> Receipt(ReceiptRequest request, CancellationToken ct)
    {
        await stock.RecordReceiptAsync(request, ActorId, ct);
        return NoContent();
    }

    [HttpPost("production")]
    public async Task<IActionResult> Production(ProductionRequest request, CancellationToken ct)
    {
        await stock.RecordProductionAsync(request, ActorId, ct);
        return NoContent();
    }

    [HttpGet("transfers")]
    public Task<IReadOnlyList<TransferDto>> Transfers([FromQuery] bool pendingOnly, CancellationToken ct)
        => stock.GetTransfersAsync(pendingOnly, ct);

    [HttpPost("transfers")]
    public async Task<IActionResult> Transfer(TransferRequest request, CancellationToken ct)
    {
        await stock.SendTransferAsync(request, ActorId, ct);
        return NoContent();
    }

    [HttpPost("transfers/{id:guid}/receive")]
    public async Task<IActionResult> ReceiveTransfer(Guid id, ReceiveTransferRequest request, CancellationToken ct)
    {
        await stock.ReceiveTransferAsync(id, request, ActorId, ct);
        return NoContent();
    }

    [HttpPost("waste")]
    public async Task<IActionResult> Waste(WasteRequest request, CancellationToken ct)
    {
        await stock.RecordWasteAsync(request, ActorId, ct);
        return NoContent();
    }

    [HttpPost("counts")]
    public async Task<IActionResult> Count(CountRequest request, CancellationToken ct)
    {
        await stock.RecordCountAsync(request, ActorId, ct);
        return NoContent();
    }

    [HttpGet("history")]
    public Task<StockHistoryDto> History([FromQuery] int page, [FromQuery] Guid? itemId,
        [FromQuery] StockLocation? location, CancellationToken ct) => stock.GetHistoryAsync(page, itemId, location, ct);
}
