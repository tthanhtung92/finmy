using System.Globalization;

using Finmy.Budgeting.Application.Caching;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class BudgetingCachePolicyTests
{

    [Theory]
    [InlineData(
        "2026-07-05 00:00:00 +00:00",
        "2026-07-20 00:00:00 +00:00",
        "tag:envelopes:summary:2026-07")]
    [InlineData(
        "2026-07-15 00:00:00 +00:00",
        "2026-08-10 00:00:00 +00:00",
        "tag:envelopes:summary:2026-07|tag:envelopes:summary:2026-08")]
    [InlineData(
        "2026-07-01 00:00:00 +00:00",
        "2026-08-01 00:00:00 +00:00",
        "tag:envelopes:summary:2026-07")]
    [InlineData(
        "2026-01-10 00:00:00 +00:00",
        "2026-04-05 00:00:00 +00:00",
        "tag:envelopes:summary:2026-01|tag:envelopes:summary:2026-02|tag:envelopes:summary:2026-03|tag:envelopes:summary:2026-04")]
    public void SummaryTagsForPeriod_ShouldReturnExpectedTags(string periodStart, string periodEnd, string expectedTagsCsv)
    {
        var periodStartUtc = DateTimeOffset.Parse(periodStart, CultureInfo.InvariantCulture);
        var periodEndUtc = DateTimeOffset.Parse(periodEnd, CultureInfo.InvariantCulture);

        var expected = string.IsNullOrEmpty(expectedTagsCsv)
            ? []
            : expectedTagsCsv.Split('|');

        var result = BudgetingCachePolicy.SummaryTagsForPeriod(periodStartUtc, periodEndUtc);

        result.ShouldBe(expected);
    }
}
