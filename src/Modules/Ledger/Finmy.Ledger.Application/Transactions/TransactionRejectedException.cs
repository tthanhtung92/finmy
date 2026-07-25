using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Application.Transactions;

public sealed class TransactionRejectedException(Error error) : Exception($"Transaction rejected: {error.Code} - {error.Description}")
{
    public Error Error { get; } = error;
}
