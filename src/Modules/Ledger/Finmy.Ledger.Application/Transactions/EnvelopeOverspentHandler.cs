using Finmy.Contracts.Budgeting;
using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;

using Microsoft.Extensions.Logging;

namespace Finmy.Ledger.Application.Transactions;

public sealed partial class EnvelopeOverspentHandler(
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
            LogTransactionMissing(logger, message.TransactionId);
            throw new TransactionNotFoundException(message.TransactionId);
        }

        var result = transaction.Reverse(timeProvider.GetUtcNow());

        await statusStore.MarkFailedAsync(message.TransactionId, TransactionErrors.Overspent(message.AttemptedAmount, message.Remaining), cancellationToken);
        LogTransactionFailed(logger, message.TransactionId);

        // A redelivered message finds the transaction already reversed. Reverse refuses the
        // second attempt, which is the expected no-op, not an error worth escalating.
        if (result.IsFailure)
            LogReverseRejected(logger, message.TransactionId, result.Error.Code);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Transaction with Id '{TransactionId}' does not exist.")]
    private static partial void LogTransactionMissing(ILogger logger, Guid transactionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transaction with Id '{TransactionId}' failed.")]
    private static partial void LogTransactionFailed(ILogger logger, Guid transactionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transaction with Id '{TransactionId}' can not reverse: '{ErrorCode}'.")]
    private static partial void LogReverseRejected(ILogger logger, Guid transactionId, string errorCode);
}
