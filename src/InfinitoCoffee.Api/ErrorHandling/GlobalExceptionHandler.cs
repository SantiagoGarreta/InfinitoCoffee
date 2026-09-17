using InfinitoCoffee.Application.Authentication.Exceptions;
using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Domain.Orders.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, logLevel) = MapException(exception);

        if (logLevel == LogLevel.Error)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.Log(logLevel, exception, "Handled exception while processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }

    private static (int Status, string Title, string Detail, LogLevel LogLevel) MapException(Exception exception)
    {
        return exception switch
        {
            InvalidCredentialsException invalidCredentials
                => (StatusCodes.Status401Unauthorized, "Authentication Failed", invalidCredentials.Message, LogLevel.Information),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Resource Not Found", notFound.Message, LogLevel.Information),
            ConflictException conflict when IsInactiveBusinessRule(conflict)
                => (StatusCodes.Status400BadRequest, "Domain Rule Violation", conflict.Message, LogLevel.Warning),
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.Message, LogLevel.Warning),
            DomainException domain => (StatusCodes.Status400BadRequest, "Domain Rule Violation", domain.Message, LogLevel.Warning),
            ArgumentException argument => (StatusCodes.Status400BadRequest, "Invalid Request", argument.Message, LogLevel.Information),
            DbUpdateException dbUpdate when TryMapDbUpdateException(dbUpdate, out var dbStatus, out var dbTitle, out var dbDetail)
                => (dbStatus, dbTitle, dbDetail, LogLevel.Warning),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.", LogLevel.Error)
        };
    }

    private static bool IsInactiveBusinessRule(ConflictException exception)
    {
        return exception.Message.Contains("inactive", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapDbUpdateException(
        DbUpdateException exception,
        out int status,
        out string title,
        out string detail)
    {
        if (exception.InnerException is SqlException sqlException
            && (sqlException.Number == 2601 || sqlException.Number == 2627 || sqlException.Number == 547))
        {
            status = StatusCodes.Status409Conflict;
            title = "Persistence Conflict";
            detail = "The requested change conflicts with persisted data constraints.";
            return true;
        }

        if (exception.InnerException is not null
            && exception.InnerException.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
            && (exception.InnerException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || exception.InnerException.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase)))
        {
            status = StatusCodes.Status409Conflict;
            title = "Persistence Conflict";
            detail = "The requested change conflicts with persisted data constraints.";
            return true;
        }

        status = default;
        title = string.Empty;
        detail = string.Empty;
        return false;
    }
}
