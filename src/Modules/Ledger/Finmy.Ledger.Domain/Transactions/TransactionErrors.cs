using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Domain.Transactions;

public static class TransactionErrors
{
    public static readonly Error AmountNotPositive = new(
        "Ledger.AmountNotPositive",
        "Transaction amount must be greater than zero.",
        ErrorType.Validation);

    public static readonly Error SpaceRequired = new(
        "Ledger.SpaceRequired",
        "Transaction must be associated with a space.",
        ErrorType.Validation);

    public static readonly Error EnvelopeRequired = new(
        "Ledger.EnvelopeRequired",
        "Transaction must be associated with an envelope.",
        ErrorType.Validation);

    public static readonly Error DirectionInvalid = new(
        "Ledger.DirectionInvalid",
        "Transaction direction is invalid.",
        ErrorType.Validation);

    public static Error NotFound(Guid transactionId)
    {
        return new(
            "Ledger.NotFound",
            $"Transaction with Id '{transactionId}' was not found.",
            ErrorType.NotFound);
    }
}
