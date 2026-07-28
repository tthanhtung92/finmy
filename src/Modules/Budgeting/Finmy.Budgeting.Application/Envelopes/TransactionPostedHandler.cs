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
    TimeProvider timeProvider,
    ILogger<TransactionPostedHandler> logger)
{
    public async Task<OutgoingMessages> HandleAsync(TransactionPostedEvent message, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(message.EnvelopeId, cancellationToken);

        if (envelope is null)
            throw new EnvelopeNotFoundException(message.EnvelopeId);

        var result = message.Direction switch
        {
            TransactionDirection.Expense => envelope.Spend(message.Amount),
            TransactionDirection.Income => envelope.Fund(message.Amount),
            _ => throw new UnreachableException("unmapped TransactionDirection")
        };
        var action = message.Direction switch
        {
            TransactionDirection.Expense => "spend",
            TransactionDirection.Income => "fund",
            _ => throw new UnreachableException("unmapped TransactionDirection")
        };

        var outgoing = new OutgoingMessages();

        if (result.IsSuccess)
        {
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
