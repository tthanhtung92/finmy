using System.Diagnostics;

using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Contracts.Budgeting;
using Finmy.Contracts.Ledger;

using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed class TransactionPostedHandler(
    IEnvelopeRepository repository,
    TimeProvider timeProvider,
    ILogger<TransactionPostedHandler> logger)
{
    public async Task<EnvelopeOverspentEvent?> HandleAsync(TransactionPostedEvent message, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(message.EnvelopeId, cancellationToken);

        if (envelope is null)
            throw new EnvelopeNotFoundException(message.EnvelopeId);

        var result = message.Direction switch
        {
            TransactionDirection.Expense => envelope.Spend(message.Amount),
            TransactionDirection.Income => envelope.Release(message.Amount),
            _ => throw new UnreachableException("unmapped TransactionDirection")
        };
        var action = message.Direction switch
        {
            TransactionDirection.Expense => "spend",
            TransactionDirection.Income => "release",
            _ => throw new UnreachableException("unmapped TransactionDirection")
        };

        if (result.IsSuccess)
        {
            logger.LogInformation("Envelope with Id '{EnvelopeId}' {Action} succeeded.", message.EnvelopeId, action);
            return null;
        }

        if (result.Error == EnvelopeErrors.InsufficientFunds)
        {
            logger.LogWarning("Envelope with Id '{EnvelopeId}' {Action} rejected: '{ErrorCode}'.", message.EnvelopeId, action, result.Error.Code);
            return new EnvelopeOverspentEvent(message.TransactionId, message.SpaceId, message.EnvelopeId, message.Amount, envelope.Remaining, timeProvider.GetUtcNow());
        }
        else
        {
            throw new InvalidOperationException(result.Error.Code);
        }
    }
}
