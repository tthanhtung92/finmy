using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Domain.Transactions;

public static class TransactionErrors
{
    public static readonly Error TransactionIdRequired = new(
        "Ledger.TransactionIdRequired",
        "Transaction must have a non-empty identifier.",
        ErrorType.Validation);

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

    public static readonly Error ErrorInvalid = new(
        "Ledger.ErrorInvalid",
        "Transaction snapshot's Error is invalid.",
        ErrorType.Failure);

    public static Error NotFound(Guid transactionId)
    {
        return new(
            "Ledger.NotFound",
            $"Transaction with Id '{transactionId}' was not found.",
            ErrorType.NotFound);
    }

    public static readonly Error AlreadyReversed = new(
        "Ledger.AlreadyReversed",
        "Transaction already reversed.",
        ErrorType.Conflict);

    public static readonly Error AlreadyConfirmed = new(
        "Ledger.AlreadyConfirmed",
        "Transaction already confirmed.",
        ErrorType.Conflict);

    public static Error Overspent(decimal attemptedAmount, decimal remaining)
    {
        return new(
            "Ledger.Overspent",
            $"Cannot spend {attemptedAmount:C}. Only {remaining:C} is remaining.",
            ErrorType.Conflict);
    }

    public static readonly Error IdempotencyKeyTooLong = new (
        "Ledger.IdempotencyKeyTooLong",
        "Idempotency key must not exceed 255 characters.",
        ErrorType.Validation);

    public static readonly Error IdempotencyKeyReused = new (
        "Ledger.IdempotencyKeyReused",
        "Idempotency key has already been used.",
        ErrorType.Unprocessable);

    public static readonly Error RequestInProgress = new(
        "Ledger.RequestInProgress",
        "Request is already in progress.",
        ErrorType.Conflict);
}
