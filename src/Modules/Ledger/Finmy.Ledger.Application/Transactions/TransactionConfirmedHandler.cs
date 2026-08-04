using Finmy.Contracts.Budgeting;
using Finmy.Ledger.Application.Abstractions;

using Microsoft.Extensions.Logging;

namespace Finmy.Ledger.Application.Transactions;

public partial class TransactionConfirmedHandler(
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
            LogConfirmRejected(logger, message.TransactionId, confirmResult.Error.Code);
            return;
        }

        await statusStore.MarkSucceededAsync(message.TransactionId, cancellationToken);
        LogConfirmed(logger, message.TransactionId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transaction with Id '{TransactionId}' confirm rejected: '{ErrorCode}'.")]
    private static partial void LogConfirmRejected(ILogger logger, Guid transactionId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transaction with Id '{TransactionId}' confirmed.")]
    private static partial void LogConfirmed(ILogger logger, Guid transactionId);
}
