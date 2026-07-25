using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;

using Microsoft.Extensions.Logging;

using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Finmy.Ledger.Application.Transactions;

public sealed class RecordTransactionHandler(ITransactionStatusStore statusStore, ILogger<RecordTransactionHandler> logger)
{
    public async Task HandleAsync(RecordTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = Transaction.Create(
            command.TransactionId, command.SpaceId,
            command.EnvelopeId, command.Amount, command.Direction,
            command.OccurredOn, command.Description);

        if (transaction.IsFailure)
        {
            await statusStore.MarkFailedAsync(command.TransactionId, transaction.Error, cancellationToken);
            logger.LogWarning("Transaction with Id '{TransactionId}' rejected: '{ErrorCode}'.", command.TransactionId, transaction.Error.Code);
            throw new TransactionRejectedException(transaction.Error);
        }

        await statusStore.MarkSucceededAsync(command.TransactionId, cancellationToken);
        logger.LogInformation("Transaction with Id '{TransactionId}' succeeded.", command.TransactionId);
    }

    public static void Configure(HandlerChain chain)
    {
        chain.OnException<TransactionRejectedException>().Discard();
    }
}
