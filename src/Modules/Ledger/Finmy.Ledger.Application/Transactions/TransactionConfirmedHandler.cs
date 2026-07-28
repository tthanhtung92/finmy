using Finmy.Contracts.Budgeting;
using Finmy.Ledger.Application.Abstractions;

using Microsoft.Extensions.Logging;

namespace Finmy.Ledger.Application.Transactions;

public class TransactionConfirmedHandler(
    ITransactionRepository repository,
    ITransactionRequestStatusStore statusStore,
    TimeProvider timeProvider,
    ILogger<TransactionConfirmedHandler> logger)
{
    public async Task HandleAsync(EnvelopeBalanceChangedEvent message, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(message.TransactionId, cancellationToken);

        if (transaction is null)
            throw new TransactionNotFoundException(message.TransactionId);

        var confirmResult = transaction.Confirm(timeProvider.GetUtcNow());

        if (confirmResult.IsFailure)
        {
            logger.LogWarning("Transaction with Id '{TransactionId}' confirm rejected: '{ErrorCode}'.", message.TransactionId, confirmResult.Error.Code);
            return;
        }

        await statusStore.MarkSucceededAsync(message.TransactionId, cancellationToken);
        logger.LogInformation("Transaction with Id '{TransactionId}' confirmed.", message.TransactionId);
    }
}
