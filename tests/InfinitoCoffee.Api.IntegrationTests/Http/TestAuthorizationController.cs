using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

[ApiController]
[Route("_integration-tests/authorization")]
public sealed class TestAuthorizationController : ControllerBase
{
    public const string ForbiddenPolicyName = "IntegrationTestForbidden";

    [Authorize(Policy = ForbiddenPolicyName)]
    [HttpGet("forbidden")]
    public IActionResult ForbiddenEndpoint()
    {
        return Ok();
    }
}
