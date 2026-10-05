using InfinitoCoffee.Application.Branches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(IBranchRepository branches) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok((await branches.GetAllAsync(ct)).Select(x => new { x.Id, Name = $"Sucursal {x.Id}" }));
}
