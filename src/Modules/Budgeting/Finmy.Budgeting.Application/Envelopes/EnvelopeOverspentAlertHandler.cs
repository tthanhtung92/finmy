using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Abstractions.Dtos;
using Finmy.Budgeting.Application.Abstractions.Enum;
using Finmy.Contracts.Budgeting;

using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public partial class EnvelopeOverspentAlertHandler(
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

        LogOverspentAlertPushed(logger, message.EnvelopeId, message.AttemptedAmount, message.Remaining);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overspent alert pushed for envelope '{EnvelopeId}': attempted {AttemptedAmount}, remaining {Remaining}.")]
    private static partial void LogOverspentAlertPushed(ILogger logger, Guid envelopeId, decimal attemptedAmount, decimal remaining);
}
