using Finmy.Contracts.Ledger;
using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;

using Microsoft.Extensions.Logging;

using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

using DomainDirection = Finmy.Ledger.Domain.Transactions.TransactionDirection;
using ContractDirection = Finmy.Contracts.Ledger.TransactionDirection;

using System.Diagnostics;

namespace Finmy.Ledger.Application.Transactions;

public sealed partial class RecordTransactionHandler(
    ITransactionRepository repository,
    ITransactionRequestStatusStore statusStore,
    ILogger<RecordTransactionHandler> logger)
{
    public async Task<TransactionPostedEvent> HandleAsync(RecordTransactionCommand command, CancellationToken cancellationToken)
    {
        var result = Transaction.Create(
            command.TransactionId,
            command.SpaceId,
            command.EnvelopeId,
            command.Amount,
            command.Direction,
            command.OccurredOn,
            command.Description);

        if (result.IsFailure)
        {
            await statusStore.MarkFailedAsync(command.TransactionId, result.Error, cancellationToken);
            LogTransactionRejected(logger, command.TransactionId, result.Error.Code);
            throw new TransactionRejectedException(result.Error);
        }

        repository.Add(result.Value);

        return new TransactionPostedEvent(
            result.Value.Id,
            result.Value.SpaceId,
            result.Value.EnvelopeId,
            result.Value.Amount,
            result.Value.Direction.ToContractDirection(),
            result.Value.OccurredOnUtc
            );
    }

    public static void Configure(HandlerChain chain)
    {
        chain.OnException<TransactionRejectedException>().Discard();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transaction with Id '{TransactionId}' rejected: '{ErrorCode}'.")]
    private static partial void LogTransactionRejected(ILogger logger, Guid transactionId, string errorCode);
}

file static class TransactionDirectionExtensions
{
    public static ContractDirection ToContractDirection(this DomainDirection direction) =>
        direction switch
        {
            DomainDirection.Expense => ContractDirection.Expense,
            DomainDirection.Income => ContractDirection.Income,
            _ => throw new UnreachableException("unmapped TransactionDirection")
        };
}
