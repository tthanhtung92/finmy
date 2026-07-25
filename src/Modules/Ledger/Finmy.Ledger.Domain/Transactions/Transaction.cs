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

    // Constructor rỗng cho EF Core (materialization qua reflection)
    private Transaction()
    {
    }

    // Constructor có tham số, dùng nội bộ trong Create
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
