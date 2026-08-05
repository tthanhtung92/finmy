using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Finmy.Api.HealthChecks;

/// <summary>
/// Readiness for Redis, reusing the IDistributedCache already registered for HybridCache's L2
/// (Program.cs) rather than opening a second connection just to probe it.
/// </summary>
public sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    private const string ProbeKey = "__finmy_health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.GetAsync(ProbeKey, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Cannot reach Redis.", ex);
        }
    }
}
