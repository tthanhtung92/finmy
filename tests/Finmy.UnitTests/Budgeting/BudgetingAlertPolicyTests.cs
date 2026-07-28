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
        // 20% của 1.000 là 200. Ngưỡng là "< 20%" (strict), nên bằng đúng 200 chưa được coi là thấp.
        // Nếu ai đó lỡ đổi "<" thành "<=" thì test này sẽ đỏ.
        var result = BudgetingAlertPolicy.IsLowBalance(allocated: 1_000m, remaining: 200m);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1_000)]
    public void IsLowBalance_WithNonPositiveAllocated_ReturnsFalse(decimal allocated)
    {
        // Envelope chưa có ngân sách (hoặc dữ liệu lỗi) thì không nên báo "sắp hết tiền".
        var result = BudgetingAlertPolicy.IsLowBalance(allocated, remaining: 0m);

        result.ShouldBeFalse();
    }
}
