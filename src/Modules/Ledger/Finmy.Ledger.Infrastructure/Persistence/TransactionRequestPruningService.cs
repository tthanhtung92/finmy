using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Finmy.Ledger.Infrastructure.Persistence;

/// <summary>
/// The rest of TECH-DEBT #3: once the status store lives in Postgres (previous commit), every
/// request leaves a row behind forever unless something sweeps it. One timer, one DELETE -- the
/// sweep is idempotent (DELETE WHERE ExpiresAtUtc &lt; now), so running it on every replica needs
/// no leader election or distributed lock.
/// </summary>
public sealed partial class TransactionRequestPruningService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TransactionRequestPruningService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, timeProvider);

        do
        {
            await PruneExpiredAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One sweep, exposed separately from ExecuteAsync's loop so it can be run directly rather
    /// than waiting out a 12-hour timer.
    /// </summary>
    public async Task<int> PruneExpiredAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        try
        {
            var now = timeProvider.GetUtcNow();
            var deleted = await dbContext.TransactionRequests
                .Where(x => x.ExpiresAtUtc < now)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
                LogPruned(logger, deleted);

            return deleted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A transient failure sweeping old rows must not take the whole host down --
            // BackgroundService's default behaviour on an unhandled exception is to stop the
            // application, which would be a self-inflicted outage over housekeeping.
            LogPruneFailed(logger, ex);
            return 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pruned {Count} expired transaction request record(s).")]
    private static partial void LogPruned(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to prune expired transaction request records.")]
    private static partial void LogPruneFailed(ILogger logger, Exception exception);
}
