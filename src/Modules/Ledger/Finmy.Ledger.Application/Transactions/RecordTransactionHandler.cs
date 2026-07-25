using Finmy.Ledger.Domain.Transactions;
using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Application.Transactions;

public sealed class RecordTransactionHandler
{
    public Result<Transaction> Handle(RecordTransactionCommand command)
    {
        var transaction = Transaction.Create(command.SpaceId, command.EnvelopeId, command.Amount, command.Direction, command.OccurredOn, command.Description);

        if (transaction.IsFailure)
            return transaction.Error;

        return transaction;
    }
}
