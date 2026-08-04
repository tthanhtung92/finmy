using Finmy.Budgeting.Application.Envelopes;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class BudgetingAlertPolicyTests
{
    [Fact]
    public void IsLowBalance_WithRemainingBelowThreshold_ReturnsTrue()
    {
        var result = BudgetingAlertPolicy.IsLowBalance(allocated: 1_000m, remaining: 100m);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsLowBalance_WithRemainingAboveThreshold_ReturnsFalse()
    {
        var result = BudgetingAlertPolicy.IsLowBalance(allocated: 1_000m, remaining: 300m);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsLowBalance_WithRemainingExactlyAtThreshold_ReturnsFalse()
    {
        // 20% of 1,000 is 200. The threshold is strictly "< 20%", so exactly 200 does not count as low.
        // If someone changes "<" to "<=", this test turns red.
        var result = BudgetingAlertPolicy.IsLowBalance(allocated: 1_000m, remaining: 200m);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1_000)]
    public void IsLowBalance_WithNonPositiveAllocated_ReturnsFalse(decimal allocated)
    {
        // An envelope with no budget (or corrupt data) should not report "running low".
        var result = BudgetingAlertPolicy.IsLowBalance(allocated, remaining: 0m);

        result.ShouldBeFalse();
    }
}
