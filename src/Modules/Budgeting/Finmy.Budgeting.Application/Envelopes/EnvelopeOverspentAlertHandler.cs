using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Abstractions.Dtos;
using Finmy.Budgeting.Application.Abstractions.Enum;
using Finmy.Contracts.Budgeting;

using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public class EnvelopeOverspentAlertHandler(
    IEnvelopeRealtimeNotifier notifier,
    ILogger<EnvelopeOverspentAlertHandler> logger)
{
    public async Task HandleAsync(EnvelopeOverspentEvent message, CancellationToken cancellationToken)
    {
        var alert = new EnvelopeAlert(
            Id: message.EnvelopeId,
            Level: EnvelopeAlertLevel.Overspent,
            Allocated: message.Allocated,
            Spent: message.Spent,
            Remaining: message.Remaining,
            AttemptedAmount: message.AttemptedAmount);

        await notifier.EnvelopeAlertAsync(alert, cancellationToken);

        logger.LogWarning("Overspent alert pushed for envelope '{EnvelopeId}': attempted {AttemptedAmount}, remaining {Remaining}.", message.EnvelopeId, message.AttemptedAmount, message.Remaining);
    }
}
