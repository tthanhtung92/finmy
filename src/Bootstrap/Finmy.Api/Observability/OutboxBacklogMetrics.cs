using System.Diagnostics.Metrics;

using Finmy.Ledger.Infrastructure.Persistence;
using Finmy.SharedKernel.Observability;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Api.Observability;

/// <summary>
/// Publishes <c>finmy.outbox.backlog</c>: the row count of Wolverine's own
/// <c>wolverine.wolverine_outgoing_envelopes</c> table. An <see cref="ObservableGauge{T}"/>
/// callback must be synchronous, and the query is not cheap enough to run inline on every
/// export tick, so a background poll refreshes a field the gauge just reads.
/// </summary>
public sealed partial class OutboxBacklogMetrics : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxBacklogMetrics> _logger;
    private readonly ObservableGauge<long> _gauge;
    private long _backlog;

    public OutboxBacklogMetrics(IServiceScopeFactory scopeFactory, ILogger<OutboxBacklogMetrics> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _gauge = FinmyTelemetry.Meter.CreateObservableGauge(
            "finmy.outbox.backlog",
            () => Interlocked.Read(ref _backlog),
            description: "Rows currently queued in Wolverine's transactional outbox.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = _gauge; // held only to keep the instrument alive for the process lifetime

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

                // SqlQueryRaw<T> for a scalar T maps whatever column is named "Value"; Postgres
                // names an unaliased count(*) "count", so the query returns no matching column
                // without this alias.
                var count = await dbContext.Database
                    .SqlQueryRaw<long>("select count(*) as \"Value\" from wolverine.wolverine_outgoing_envelopes")
                    .SingleAsync(stoppingToken);

                Interlocked.Exchange(ref _backlog, count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort telemetry: a transient DB hiccup here must never crash the host,
                // and there is nothing an alert needs from a single missed poll.
                LogPollFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to poll the outbox backlog")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
