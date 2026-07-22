using Microsoft.Extensions.Caching.Hybrid;

namespace Finmy.Budgeting.Application.Caching;

public static class BudgetingCachePolicy
{
    public const string EnvelopeListKeyPrefix = "envelopes:list";
    public const string EnvelopeListTag = "tag:envelopes:list";
    public static readonly HybridCacheEntryOptions EnvelopeListEntry = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };

    public const string EnvelopeSummaryKeyPrefix = "envelopes:summary";
    public static string SummaryTag(int year, int month) => $"tag:envelopes:summary:{year}-{month:D2}";
    public static IReadOnlyList<string> SummaryTagsForPeriod(DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc)
    {
        var summary = new List<string>();

        var cursor = new DateTimeOffset(periodStartUtc.Year, periodStartUtc.Month, 1, 0, 0, 0, TimeSpan.Zero);

        while (cursor < periodEndUtc)
        {
            summary.Add(SummaryTag(cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        return summary;
    }
    public static readonly HybridCacheEntryOptions MonthlySummaryEntry = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
}
