using InfinitoCoffee.Api.Configuration;
using InfinitoCoffee.Api.Contracts;
using InfinitoCoffee.Api.Contracts.Orders;
using InfinitoCoffee.Application.Orders.Commands;
using InfinitoCoffee.Application.Orders.Queries;
using InfinitoCoffee.Application.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly IOptions<PickupDisplayOptions> _pickupDisplayOptions;

    public OrdersController(OrderService orderService, IOptions<PickupDisplayOptions> pickupDisplayOptions)
    {
        _orderService = orderService;
        _pickupDisplayOptions = pickupDisplayOptions;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.CreateOrderAsync(
            new CreateOrderCommand(
                request.OrderNumber,
                ApiContractMapper.ParseOrderSource(request.Source),
                request.Notes,
                request.Items.Select(item => new CreateOrderItemCommand(item.ProductId, item.Quantity, item.Notes)).ToArray()),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ApiContractMapper.MapOrder(order));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByIdAsync(new GetOrderByIdQuery(id), cancellationToken);
        return Ok(ApiContractMapper.MapOrder(order));
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyCollection<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<OrderResponse>>> GetActive(CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetActiveOrdersAsync(cancellationToken);
        return Ok(orders.Select(ApiContractMapper.MapOrder).ToArray());
    }

    [HttpGet("pickup")]
    [ProducesResponseType(typeof(IReadOnlyCollection<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<OrderResponse>>> GetPickup(CancellationToken cancellationToken)
    {
        var visibility = TimeSpan.FromMinutes(_pickupDisplayOptions.Value.ReadyVisibilityMinutes);
        var orders = await _orderService.GetPickupOrdersAsync(new GetPickupOrdersQuery(visibility), cancellationToken);
        return Ok(orders.Select(ApiContractMapper.MapOrder).ToArray());
    }

    [HttpPost("{id:guid}/start-preparation")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> StartPreparation(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.StartOrderPreparationAsync(new StartOrderPreparationCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapOrder(order));
    }

    [HttpPost("{id:guid}/mark-ready")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> MarkReady(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.MarkOrderAsReadyAsync(new MarkOrderAsReadyCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapOrder(order));
    }

    [HttpPost("{id:guid}/deliver")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Deliver(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.DeliverOrderAsync(new DeliverOrderCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapOrder(order));
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.CancelOrderAsync(new CancelOrderCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapOrder(order));
    }
}
