using Finmy.Budgeting.Domain.Envelopes;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class EnvelopeSpendTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static Envelope CreateEnvelope(decimal allocated)
        => Envelope.Create("Monthly shopping expenses", "Buy clothes", CategoryId, allocated, PeriodStart, PeriodEnd).Value;

    [Fact]
    public void Spend_WithAmountBelowRemaining_ReducesRemainingAndBumpsVersion()
    {
        var envelope = CreateEnvelope(1_500m);
        var versionBeforeSpend = envelope.Version;

        var result = envelope.Spend(500m);

        result.IsSuccess.ShouldBeTrue();
        envelope.Spent.ShouldBe(500m);
        envelope.Remaining.ShouldBe(1_000m);
        envelope.Version.ShouldBe(versionBeforeSpend + 1);
    }

    [Fact]
    public void Spend_WithAmountEqualToRemaining_ReturnsSuccess()
    {
        var envelope = CreateEnvelope(1_500m);

        var result = envelope.Spend(1_500m);

        result.IsSuccess.ShouldBeTrue();
        envelope.Spent.ShouldBe(1_500m);
        envelope.Remaining.ShouldBe(0m);
    }

    [Fact]
    public void Spend_WithAmountAboveRemaining_ReturnsInsufficientFund()
    {
        var envelope = CreateEnvelope(1_500m);

        var result = envelope.Spend(1_500.01m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.InsufficientFunds);
    }

    [Fact]
    public void Spend_WhenRejected_LeavesSpentAndVersionUnchange()
    {
        var envelope = CreateEnvelope(1_500m);
        var spentBeforeSpend = envelope.Spent;
        var versionBeforeSpend = envelope.Version;

        var result = envelope.Spend(1_500.01m);

        result.IsFailure.ShouldBeTrue();
        envelope.Spent.ShouldBe(spentBeforeSpend);
        envelope.Version.ShouldBe(versionBeforeSpend);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Spend_WithNonPositiveAmount_ReturnsSpendAmountNotPositive(decimal amount)
    {
        var envelope = CreateEnvelope(1_500m);

        var result = envelope.Spend(amount);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.SpendAmountNotPositive);
    }

    [Fact]
    public void Release_WithAmountBelowSpent_ReducesSpentAndBumpsVersion()
    {
        var envelope = CreateEnvelope(1_500m);
        envelope.Spend(1_000m);
        var versionBeforeRelease = envelope.Version;

        var result = envelope.Release(400m);

        result.IsSuccess.ShouldBeTrue();
        envelope.Spent.ShouldBe(600m);
        envelope.Version.ShouldBe(versionBeforeRelease + 1);
    }

    [Fact]
    public void Release_WithAmountAboveSpent_ReturnsRefundExceedsSpent()
    {
        var envelope = CreateEnvelope(1_500m);
        envelope.Spend(1_000m);

        var result = envelope.Release(1_000.01m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.RefundExceedsSpent);
    }

    [Fact]
    public void Update_WithAllocatedBelowSpent_ReturnsAllocatedBelowSpent()
    {
        var envelope = CreateEnvelope(1_500m);
        envelope.Spend(1_000m);

        var result = envelope.Update(
            "Monthly shopping expenses",
            "Buy clothes",
            CategoryId,
            900m,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.AllocatedBelowSpent);
    }

    [Fact]
    public void Update_OnSuccess_BumpsVersion()
    {
        var envelope = CreateEnvelope(1_500m);
        var versionBeforeUpdate = envelope.Version;

        var result = envelope.Update(
            "Monthly shopping expenses",
            "Buy clothes",
            CategoryId,
            2_000m,
            PeriodStart,
            PeriodEnd);

        result.IsSuccess.ShouldBeTrue();
        envelope.Version.ShouldBe(versionBeforeUpdate + 1);
    }
}
