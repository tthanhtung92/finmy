using Finmy.Ledger.Domain.Transactions;

using Shouldly;

namespace Finmy.UnitTests.Ledger;

public class TransactionTests
{
    private static readonly Guid SpaceId = Guid.CreateVersion7();
    private static readonly Guid EnvelopeId = Guid.CreateVersion7();
    private static readonly DateTimeOffset OccurredOn = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            250m,
            TransactionDirection.Expense,
            OccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeFalse();
        result.Value.SpaceId.ShouldBe(SpaceId);
        result.Value.EnvelopeId.ShouldBe(EnvelopeId);
        result.Value.Amount.ShouldBe(250m);
        result.Value.Direction.ShouldBe(TransactionDirection.Expense);
        result.Value.Description.ShouldBe("Groceries");
    }

    [Fact]
    public void Create_WithValidData_GeneratesNonEmptyId()
    {
        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            250m,
            TransactionDirection.Expense,
            OccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeFalse();
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithLocalOccurredOn_ConvertsToUtc()
    {
        var localOccurredOn = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.FromHours(7));

        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            250m,
            TransactionDirection.Expense,
            localOccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeFalse();
        result.Value.OccurredOnUtc.ShouldBe(localOccurredOn.ToUniversalTime());
        result.Value.OccurredOnUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyDescription_ReturnsNullDescription(string? description)
    {
        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            250m,
            TransactionDirection.Expense,
            OccurredOn,
            description);

        result.IsFailure.ShouldBeFalse();
        result.Value.Description.ShouldBeNull();
    }

    [Fact]
    public void Create_WithPaddedDescription_TrimsDescription()
    {
        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            250m,
            TransactionDirection.Expense,
            OccurredOn,
            "  Groceries  ");

        result.IsFailure.ShouldBeFalse();
        result.Value.Description.ShouldBe("Groceries");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_WithInvalidAmount_ReturnsFailureWithoutThrowing(decimal amount)
    {
        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            amount,
            TransactionDirection.Expense,
            OccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.AmountNotPositive);
    }

    [Fact]
    public void Create_WithEmptySpaceId_ReturnsFailureWithoutThrowing()
    {
        var result = Transaction.Create(
            Guid.Empty,
            EnvelopeId,
            250m,
            TransactionDirection.Expense,
            OccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.SpaceRequired);
    }

    [Fact]
    public void Create_WithEmptyEnvelopeId_ReturnsFailureWithoutThrowing()
    {
        var result = Transaction.Create(
            SpaceId,
            Guid.Empty,
            250m,
            TransactionDirection.Expense,
            OccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.EnvelopeRequired);
    }

    [Fact]
    public void Create_WithInvalidDirection_ReturnsFailureWithoutThrowing()
    {
        var invalidDirection = (TransactionDirection)999;

        var result = Transaction.Create(
            SpaceId,
            EnvelopeId,
            250m,
            invalidDirection,
            OccurredOn,
            "Groceries");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.DirectionInvalid);
    }
}
