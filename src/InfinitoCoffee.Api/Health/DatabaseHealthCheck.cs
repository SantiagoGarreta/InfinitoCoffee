using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InfinitoCoffee.Api.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly InfinitoCoffeeDbContext _dbContext;

    public DatabaseHealthCheck(InfinitoCoffeeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is available.")
                : HealthCheckResult.Unhealthy("Database connection is unavailable.");
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("Database connection check failed.", exception);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database connection check failed.", exception);
        }
    }
}
