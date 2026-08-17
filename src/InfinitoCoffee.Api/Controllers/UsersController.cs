using System.Security.Claims;
using InfinitoCoffee.Api.Authorization;
using InfinitoCoffee.Api.Contracts;
using InfinitoCoffee.Api.Contracts.Users;
using InfinitoCoffee.Application.Users.Commands;
using InfinitoCoffee.Application.Users.Queries;
using InfinitoCoffee.Application.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly UserAdministrationService _service;

    public UsersController(UserAdministrationService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _service.GetUsersAsync(cancellationToken);
        return Ok(users.Select(ApiContractMapper.MapUser).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _service.GetUserByIdAsync(new GetUserByIdQuery(id), cancellationToken);
        return Ok(ApiContractMapper.MapUser(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _service.CreateUserAsync(
            new CreateUserCommand(request.Username, request.DisplayName, request.Password, request.Role!.Value),
            cancellationToken);
        var response = ApiContractMapper.MapUser(user);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActingUserId(out var actingUserId)) return Unauthorized();
        var user = await _service.UpdateUserAsync(
            new UpdateUserCommand(id, actingUserId, request.Username, request.DisplayName, request.Role!.Value),
            cancellationToken);
        return Ok(ApiContractMapper.MapUser(user));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<UserResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var user = await _service.ActivateUserAsync(new ActivateUserCommand(id), cancellationToken);
        return Ok(ApiContractMapper.MapUser(user));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<UserResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActingUserId(out var actingUserId)) return Unauthorized();
        var user = await _service.DeactivateUserAsync(new DeactivateUserCommand(id, actingUserId), cancellationToken);
        return Ok(ApiContractMapper.MapUser(user));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await _service.ResetPasswordAsync(new ResetUserPasswordCommand(id, request.NewPassword), cancellationToken);
        return NoContent();
    }

    private bool TryGetActingUserId(out Guid actingUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actingUserId);
    }
}
