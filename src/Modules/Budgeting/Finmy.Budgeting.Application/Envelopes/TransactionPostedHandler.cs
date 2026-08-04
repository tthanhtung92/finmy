using System.Diagnostics;

using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Contracts.Budgeting;
using Finmy.Contracts.Ledger;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed class TransactionPostedHandler(
    IEnvelopeRepository repository,
    IProcessedTransactionStore processedTransactionStore,
    TimeProvider timeProvider,
    ILogger<TransactionPostedHandler> logger)
{
    public async Task<OutgoingMessages> HandleAsync(TransactionPostedEvent message, CancellationToken cancellationToken)
    {
        var outgoing = new OutgoingMessages();

        var wasApplied = await processedTransactionStore.WasAppliedAsync(message.TransactionId, cancellationToken);

        if (wasApplied)
        {
            logger.LogInformation("Transaction with Id '{TransactionId}' was already applied.", message.TransactionId);

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

            logger.LogInformation("Envelope with Id '{EnvelopeId}' {Action} succeeded.", message.EnvelopeId, action);

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
            logger.LogWarning("Envelope with Id '{EnvelopeId}' {Action} rejected: '{ErrorCode}'.", message.EnvelopeId, action, result.Error.Code);

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
}
