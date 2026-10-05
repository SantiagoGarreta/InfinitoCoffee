using System.Security.Claims;
using InfinitoCoffee.Application.Branches;
using InfinitoCoffee.Application.Users.Contracts;
using InfinitoCoffee.Domain.Branches;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.Branches;

// Every operational HTTP request and SignalR connection has one validated branch.
public sealed class BranchScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, BranchContext scope, IBranchRepository branches, IUserRepository users)
    {
        var path = http.Request.Path;
        if (!path.StartsWithSegments("/api/orders") && !path.StartsWithSegments("/api/stock")
            && !path.StartsWithSegments("/hubs/orders") && !path.StartsWithSegments("/hubs/pickup"))
        {
            await next(http);
            return;
        }

        var requested = http.Request.Headers["X-Branch-Id"].ToString();
        var query = http.Request.Query["branchId"].ToString();
        if (requested.Length > 0 && query.Length > 0 && requested != query)
            throw new ArgumentException("La sucursal de la consulta no coincide con la seleccionada.");
        if (requested.Length == 0) requested = query;
        int? branchId = null;
        if (requested.Length > 0)
        {
            if (!int.TryParse(requested, out var parsed) || parsed <= 0)
                throw new ArgumentException("Sucursal inválida.");
            branchId = parsed;
        }

        // Pickup remains public, even when the browser has a staff session.
        var publicPickup = path.StartsWithSegments("/api/orders/pickup") || path.StartsWithSegments("/hubs/pickup");
        if (!publicPickup && http.User.Identity?.IsAuthenticated == true)
        {
            var id = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = Guid.TryParse(id, out var userId) ? await users.GetByIdAsync(userId, http.RequestAborted) : null;
            if (user is null || !user.IsActive || !http.User.IsInRole(user.Role.ToString()))
            {
                await Reject(http, 401, "Tu sesión debe renovarse.");
                return;
            }
            if (user.Role != UserRole.Administrator
                && int.TryParse(http.User.FindFirstValue("branch_id"), out var sessionBranchId)
                && sessionBranchId != user.BranchId)
            {
                await Reject(http, 401, "Tu sucursal asignada cambió. Volvé a iniciar sesión.");
                return;
            }
            if (user.Role != UserRole.Administrator && branchId.HasValue && branchId != user.BranchId)
            {
                await Reject(http, 403, "No tenés acceso a esa sucursal.");
                return;
            }
            branchId ??= user.BranchId;
        }

        scope.BranchId = branchId ?? Branch.DefaultId;
        if (await branches.GetByIdAsync(scope.BranchId, http.RequestAborted) is null)
            throw new ArgumentException("La sucursal no existe.");
        http.Items["BranchId"] = scope.BranchId;
        await next(http);
    }

    private static async Task Reject(HttpContext http, int status, string detail)
    {
        http.Response.StatusCode = status;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new() { Status = status, Title = "Acceso a sucursal", Detail = detail }
        });
    }
}
