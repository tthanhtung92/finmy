using Finmy.Budgeting.Application.Abstractions;
using Finmy.Contracts.Budgeting;

using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed class EnvelopeBalanceChangedHandler(
    IEnvelopeCacheInvalidator invalidator,
    ILogger<EnvelopeBalanceChangedHandler> logger)
{
    public async Task HandleAsync(EnvelopeBalanceChangedEvent message, CancellationToken cancellationToken)
    {
        await invalidator.InvalidateAsync(message.PeriodStartUtc, message.PeriodEndUtc, cancellationToken);
        logger.LogInformation("Cache invalidated for envelope '{EnvelopeId}' period {PeriodStartUtc:yyyy-MM}.", message.EnvelopeId, message.PeriodStartUtc);
    }
}
