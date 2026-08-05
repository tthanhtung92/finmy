using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Finmy.Api.HealthChecks;

/// <summary>
/// Readiness for one module's database: the connection has to work, and the schema has to be
/// fully migrated. A replica that starts against a database mid rolling-deploy (TECH-DEBT #16)
/// fails this check instead of serving traffic against a schema it does not match.
/// </summary>
public sealed class DbContextHealthCheck<TContext>(TContext dbContext) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy($"Cannot connect to the {typeof(TContext).Name} database.");
        }

        var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        var pendingList = pending as IReadOnlyCollection<string> ?? [.. pending];

        return pendingList.Count > 0
            ? HealthCheckResult.Unhealthy(
                $"{typeof(TContext).Name} has {pendingList.Count} pending migration(s): {string.Join(", ", pendingList)}.")
            : HealthCheckResult.Healthy();
    }
}
