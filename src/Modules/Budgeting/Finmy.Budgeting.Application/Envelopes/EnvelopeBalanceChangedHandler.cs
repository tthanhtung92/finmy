using Finmy.Budgeting.Application.Abstractions;
using Finmy.Contracts.Budgeting;

using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed partial class EnvelopeBalanceChangedHandler(
    IEnvelopeCacheInvalidator invalidator,
    ILogger<EnvelopeBalanceChangedHandler> logger)
{
    public async Task HandleAsync(EnvelopeBalanceChangedEvent message, CancellationToken cancellationToken)
    {
        await invalidator.InvalidateAsync(message.PeriodStartUtc, message.PeriodEndUtc, cancellationToken);
        LogCacheInvalidated(logger, message.EnvelopeId, message.PeriodStartUtc);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache invalidated for envelope '{EnvelopeId}' period {PeriodStartUtc:yyyy-MM}.")]
    private static partial void LogCacheInvalidated(ILogger logger, Guid envelopeId, DateTimeOffset periodStartUtc);
}
