using InfinitoCoffee.Api.Authorization;
using InfinitoCoffee.Application.Branches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(IBranchRepository branches) : ControllerBase
{
    // Public names also identify each pickup screen.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok((await branches.GetAllAsync(ct)).Select(x => new { x.Id, x.Name }));

    [Authorize(Policy = AuthorizationPolicyNames.AdministratorOnly)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, RenameBranchRequest request, CancellationToken ct)
    {
        var branch = await branches.GetByIdAsync(id, ct) ?? throw new ArgumentException("La sucursal no existe.");
        branch.Rename(request.Name);
        await branches.SaveChangesAsync(ct);
        return Ok(new { branch.Id, branch.Name });
    }
}

public sealed record RenameBranchRequest(string Name);
