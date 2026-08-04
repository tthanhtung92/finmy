using Finmy.SharedKernel.Extensions;
using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Domain.Transactions;

public sealed class Transaction
{
    public Guid Id { get; private set; }
    public Guid SpaceId { get; private set; }
    public Guid EnvelopeId { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionDirection Direction { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public string? Description { get; private set; }
    public TransactionState State { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    // Parameterless constructor for EF Core materialisation through reflection
    private Transaction()
    {
    }

    // Parameterised constructor, used internally by Create
    private Transaction(
        Guid id, Guid spaceId, Guid envelopeId,
        decimal amount, TransactionDirection direction,
        DateTimeOffset occurredOnUtc, string? description)
    {
        Id = id;
        SpaceId = spaceId;
        EnvelopeId = envelopeId;
        Amount = amount;
        Direction = direction;
        OccurredOnUtc = occurredOnUtc;
        Description = description;
        State = TransactionState.Posted;
        ReversedAtUtc = null;
    }

    public static Result<Transaction> Create(
        Guid id, Guid spaceId, Guid envelopeId,
        decimal amount, TransactionDirection direction,
        DateTimeOffset occurredOn, string? description)
    {
        occurredOn = occurredOn.ToUniversalTime();

        var validateResult = Validate(id, spaceId, envelopeId, amount, direction);

        if (validateResult.IsFailure)
        {
            return validateResult.Error;
        }

        return new Transaction
        (
            id,
            spaceId,
            envelopeId,
            amount,
            direction,
            occurredOn,
            description?.TrimOrNull()
        );
    }

    public Result Reverse(DateTimeOffset reversedAtUtc)
    {
        if (State == TransactionState.Reversed)
            return TransactionErrors.AlreadyReversed;

        State = TransactionState.Reversed;
        ReversedAtUtc = reversedAtUtc.ToUniversalTime();

        return Result.Success();
    }

    public Result Confirm(DateTimeOffset confirmedAtUtc)
    {
        if (State == TransactionState.Reversed)
            return TransactionErrors.AlreadyReversed;

        if (State == TransactionState.Confirmed)
            return TransactionErrors.AlreadyConfirmed;

        State = TransactionState.Confirmed;
        ConfirmedAtUtc = confirmedAtUtc.ToUniversalTime();

        return Result.Success();
    }

    private static Result Validate(
        Guid id, Guid spaceId, Guid envelopeId,
        decimal amount, TransactionDirection direction)
    {
        if (id == Guid.Empty)
            return Result.Failure(TransactionErrors.TransactionIdRequired);

        if (spaceId == Guid.Empty)
            return Result.Failure(TransactionErrors.SpaceRequired);

        if (envelopeId == Guid.Empty)
            return Result.Failure(TransactionErrors.EnvelopeRequired);

        if (amount <= 0m)
            return Result.Failure(TransactionErrors.AmountNotPositive);

        if (!Enum.IsDefined(direction))
            return Result.Failure(TransactionErrors.DirectionInvalid);

        return Result.Success();
    }
}
