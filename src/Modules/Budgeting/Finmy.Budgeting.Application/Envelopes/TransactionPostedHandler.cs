using System.Diagnostics;

using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Contracts.Budgeting;
using Finmy.Contracts.Ledger;
using Finmy.SharedKernel.Observability;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed partial class TransactionPostedHandler(
    IEnvelopeRepository repository,
    IProcessedTransactionStore processedTransactionStore,
    TimeProvider timeProvider,
    ILogger<TransactionPostedHandler> logger)
{
    public async Task<OutgoingMessages> HandleAsync(TransactionPostedEvent message, CancellationToken cancellationToken)
    {
        using var activity = FinmyTelemetry.AntiOverspend.StartActivity("budgeting.apply_transaction");
        activity?.SetTag("transaction.id", message.TransactionId);
        activity?.SetTag("envelope.id", message.EnvelopeId);

        var outgoing = new OutgoingMessages();

        var wasApplied = await processedTransactionStore.WasAppliedAsync(message.TransactionId, cancellationToken);

        if (wasApplied)
        {
            LogAlreadyApplied(logger, message.TransactionId);
            activity?.SetTag("outcome", "duplicate");

            return outgoing;
        }

        var envelope = await repository.GetByIdAsync(message.EnvelopeId, cancellationToken);

        if (envelope is null)
            throw new EnvelopeNotFoundException(message.EnvelopeId);

        var (result, action) = message.Direction switch
        {
            TransactionDirection.Expense => (envelope.Spend(message.Amount), "spend"),
            TransactionDirection.Income => (envelope.Fund(message.Amount), "fund"),
            _ => throw new UnreachableException("unmapped TransactionDirection")
        };

        if (result.IsSuccess)
        {
            processedTransactionStore.MarkApplied(message.TransactionId, message.EnvelopeId, message.Amount, timeProvider.GetUtcNow());

            LogEnvelopeChanged(logger, message.EnvelopeId, action);
            activity?.SetTag("outcome", "applied");
            activity?.SetTag("envelope.remaining", envelope.Remaining);

            outgoing.Add(new EnvelopeBalanceChangedEvent(
                message.TransactionId,
                message.SpaceId,
                message.EnvelopeId,
                envelope.Name,
                message.Amount,
                envelope.Allocated,
                envelope.Spent,
                envelope.Remaining,
                envelope.PeriodStartUtc,
                envelope.PeriodEndUtc,
                timeProvider.GetUtcNow()
                ));

            return outgoing;
        }

        if (result.Error == EnvelopeErrors.InsufficientFunds)
        {
            LogEnvelopeChangeRejected(logger, message.EnvelopeId, action, result.Error.Code);
            activity?.SetTag("outcome", "overspent");
            activity?.SetTag("envelope.remaining", envelope.Remaining);
            FinmyTelemetry.EnvelopesOverspent.Add(1, new KeyValuePair<string, object?>("action", action));

            outgoing.Add(new EnvelopeOverspentEvent(
                message.TransactionId,
                message.SpaceId,
                message.EnvelopeId,
                message.Amount,
                envelope.Allocated,
                envelope.Spent,
                envelope.Remaining,
                timeProvider.GetUtcNow()));

            return outgoing;
        }
        else
        {
            throw new InvalidOperationException(result.Error.Code);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Transaction with Id '{TransactionId}' was already applied.")]
    private static partial void LogAlreadyApplied(ILogger logger, Guid transactionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Envelope with Id '{EnvelopeId}' {Action} succeeded.")]
    private static partial void LogEnvelopeChanged(ILogger logger, Guid envelopeId, string action);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Envelope with Id '{EnvelopeId}' {Action} rejected: '{ErrorCode}'.")]
    private static partial void LogEnvelopeChangeRejected(ILogger logger, Guid envelopeId, string action, string errorCode);
}