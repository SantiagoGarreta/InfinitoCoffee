using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace InfinitoCoffee.Api.Authentication;

public sealed class ApiCookieAuthenticationEvents : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        return WriteProblemDetailsAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            "Authentication Required",
            "Authentication is required to access this resource.");
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        return WriteProblemDetailsAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Access Denied",
            "The authenticated user is not authorized to access this resource.");
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail)
    {
        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }
}
