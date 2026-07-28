using Finmy.Budgeting.Domain.Envelopes;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class EnvelopeFundTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static Envelope CreateEnvelope(decimal allocated)
        => Envelope.Create("Monthly shopping expenses", "Buy clothes", CategoryId, allocated, PeriodStart, PeriodEnd).Value;

    [Fact]
    public void Fund_WithPositiveAmount_IncreasesAllocated()
    {
        var envelope = CreateEnvelope(3_000_000m);

        var result = envelope.Fund(500_000m);

        result.IsSuccess.ShouldBeTrue();
        envelope.Allocated.ShouldBe(3_500_000m);
        envelope.Spent.ShouldBe(0m);
        envelope.Remaining.ShouldBe(3_500_000m);
    }

    [Fact]
    public void Fund_WithPositiveAmount_BumpsVersion()
    {
        var envelope = CreateEnvelope(3_000_000m);
        var versionBeforeFund = envelope.Version;

        var result = envelope.Fund(500_000m);

        result.IsSuccess.ShouldBeTrue();
        envelope.Version.ShouldBe(versionBeforeFund + 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Fund_WithNonPositiveAmount_ReturnsFailure(decimal amount)
    {
        var envelope = CreateEnvelope(3_000_000m);
        var allocatedBeforeFund = envelope.Allocated;
        var versionBeforeFund = envelope.Version;

        var result = envelope.Fund(amount);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.FundAmountNotPositive);
        envelope.Allocated.ShouldBe(allocatedBeforeFund);
        envelope.Version.ShouldBe(versionBeforeFund);
    }

    [Fact]
    public void Fund_OnEnvelopeWithSpending_KeepsSpentUntouched()
    {
        var envelope = CreateEnvelope(3_000_000m);
        envelope.Spend(1_000_000m);

        var result = envelope.Fund(500_000m);

        result.IsSuccess.ShouldBeTrue();
        envelope.Spent.ShouldBe(1_000_000m);
    }
}
