using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Abstractions.Dtos;
using Finmy.Budgeting.Application.Abstractions.Enum;
using Finmy.Contracts.Budgeting;

using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public class EnvelopeBalanceChangedRealtimeHandler(
    IEnvelopeRealtimeNotifier notifier,
    ILogger<EnvelopeBalanceChangedRealtimeHandler> logger)
{
    public async Task HandleAsync(EnvelopeBalanceChangedEvent message, CancellationToken cancellationToken)
    {
        var snapshot = new EnvelopeBalanceSnapshot(
            message.EnvelopeId,
            message.Name,
            message.Allocated,
            message.Spent,
            message.Remaining);

        await notifier.EnvelopeUpdatedAsync(snapshot, cancellationToken);

        logger.LogInformation("Realtime balance pushed for envelope '{EnvelopeId}': remaining {Remaining} of {Allocated}.", message.EnvelopeId, message.Remaining, message.Allocated);

        if (BudgetingAlertPolicy.IsLowBalance(message.Allocated, message.Remaining))
        {
            var alert = new EnvelopeAlert(
                message.EnvelopeId,
                EnvelopeAlertLevel.LowBalance,
                message.Allocated,
                message.Spent,
                message.Remaining,
                null
                );

            await notifier.EnvelopeAlertAsync(alert, cancellationToken);

            logger.LogWarning("Envelope '{EnvelopeId}' low balance alert pushed: remaining {Remaining} of {Allocated}.", message.EnvelopeId, message.Remaining, message.Allocated);
        }
    }
}
