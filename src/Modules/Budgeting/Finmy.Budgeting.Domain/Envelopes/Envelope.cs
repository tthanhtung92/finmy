using Finmy.SharedKernel.Extensions;
using Finmy.SharedKernel.Results;

namespace Finmy.Budgeting.Domain.Envelopes;

public sealed class Envelope
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public decimal Allocated { get; private set; }
    public DateTimeOffset PeriodStartUtc { get; private set; }
    public DateTimeOffset PeriodEndUtc { get; private set; }
    public decimal Spent { get; private set; }
    public int Version { get; private set; }
    public decimal Remaining => Allocated - Spent;

    private Envelope()
    {
    }

    private Envelope(
        Guid id, string name, string? description,
        Guid categoryId, decimal allocated,
        DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        Id = id;
        Name = name;
        Description = description;
        CategoryId = categoryId;
        Allocated = allocated;
        PeriodStartUtc = periodStart;
        PeriodEndUtc = periodEnd;
    }

    public static Result<Envelope> Create(
        string name, string? description,
        Guid categoryId, decimal allocated,
        DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        periodStart = periodStart.ToUniversalTime();
        periodEnd = periodEnd.ToUniversalTime();

        var validateResult = Validate(name, categoryId, allocated, periodStart, periodEnd);
        if (validateResult.IsFailure)
            return validateResult.Error;

        return new Envelope
        (
            Guid.CreateVersion7(),
            name.Trim(),
            description?.TrimOrNull(),
            categoryId,
            allocated,
            periodStart,
            periodEnd
        );
    }

    public Result Update(
        string name, string? description,
        Guid categoryId, decimal allocated,
        DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        periodStart = periodStart.ToUniversalTime();
        periodEnd = periodEnd.ToUniversalTime();

        var validateResult = Validate(name, categoryId, allocated, periodStart, periodEnd);
        if (validateResult.IsFailure)
            return validateResult;
        if (allocated < Spent)
            return Result.Failure(EnvelopeErrors.AllocatedBelowSpent);

        Name = name.Trim();
        Description = description?.TrimOrNull();
        CategoryId = categoryId;
        Allocated = allocated;
        PeriodStartUtc = periodStart;
        PeriodEndUtc = periodEnd;
        Version++;

        return Result.Success();
    }

    public Result Spend(decimal amount)
    {
        if (amount <= 0)
            return Result.Failure(EnvelopeErrors.SpendAmountNotPositive);

        if (amount > Remaining)
            return Result.Failure(EnvelopeErrors.InsufficientFunds);

        Spent += amount;
        Version++;

        return Result.Success();
    }

    public Result Release(decimal amount)
    {
        if (amount <= 0)
            return Result.Failure(EnvelopeErrors.SpendAmountNotPositive);

        if (amount > Spent)
            return Result.Failure(EnvelopeErrors.RefundExceedsSpent);

        Spent -= amount;
        Version++;

        return Result.Success();
    }

    public Result Fund(decimal amount)
    {
        if (amount <= 0)
            return Result.Failure(EnvelopeErrors.FundAmountNotPositive);

        Allocated += amount;
        Version++;

        return Result.Success();
    }

    private static Result Validate(
        string name, Guid categoryId, decimal allocated,
        DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(EnvelopeErrors.NameEmpty);

        if (name.Trim().Length > 200)
            return Result.Failure(EnvelopeErrors.NameTooLong);

        if (categoryId == Guid.Empty)
            return Result.Failure(EnvelopeErrors.CategoryRequired);

        if (periodEnd <= periodStart)
            return Result.Failure(EnvelopeErrors.PeriodInvalid);

        if (allocated <= 0m)
            return Result.Failure(EnvelopeErrors.AllocatedNotPositive);

        return Result.Success();
    }
}
