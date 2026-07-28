using Finmy.Contracts.Budgeting;
using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;

using Microsoft.Extensions.Logging;

namespace Finmy.Ledger.Application.Transactions;

public sealed class EnvelopeOverspentHandler(
    ITransactionRepository repository,
    ITransactionRequestStatusStore statusStore,
    TimeProvider timeProvider,
    ILogger<EnvelopeOverspentHandler> logger)
{
    public async Task HandleAsync(EnvelopeOverspentEvent message, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(message.TransactionId, cancellationToken);

        if (transaction is null)
        {
            logger.LogError("Transaction with Id '{TransactionId}' does not exist.", message.TransactionId);
            throw new TransactionNotFoundException(message.TransactionId);
        }

        var result = transaction.Reverse(timeProvider.GetUtcNow());

        await statusStore.MarkFailedAsync(message.TransactionId, TransactionErrors.Overspent(message.AttemptedAmount, message.Remaining), cancellationToken);
        logger.LogWarning("Transaction with Id '{TransactionId}' failed.", message.TransactionId);

        if (result.IsFailure)
        {
            logger.LogInformation("Transaction with Id '{TransactionId}' can not reverse: '{ErrorCode}'.", message.TransactionId, result.Error.Code);
            return;
        }
    }
}
