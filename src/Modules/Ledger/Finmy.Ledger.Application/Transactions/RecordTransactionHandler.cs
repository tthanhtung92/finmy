using Finmy.Contracts.Ledger;
using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;
using Finmy.SharedKernel.Observability;

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
        using var activity = FinmyTelemetry.AntiOverspend.StartActivity("ledger.record_transaction");
        activity?.SetTag("transaction.id", command.TransactionId);
        activity?.SetTag("envelope.id", command.EnvelopeId);
        activity?.SetTag("transaction.direction", command.Direction.ToString());

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
            activity?.SetTag("outcome", "rejected");
            FinmyTelemetry.TransactionsRecorded.Add(1, new KeyValuePair<string, object?>("outcome", "rejected"));
            throw new TransactionRejectedException(result.Error);
        }

        repository.Add(result.Value);

        activity?.SetTag("outcome", "accepted");
        FinmyTelemetry.TransactionsRecorded.Add(1,
            new KeyValuePair<string, object?>("outcome", "accepted"),
            new KeyValuePair<string, object?>("direction", command.Direction.ToString()));

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
