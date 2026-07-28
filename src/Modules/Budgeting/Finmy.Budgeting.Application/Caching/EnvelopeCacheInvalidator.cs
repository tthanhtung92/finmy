using Finmy.Budgeting.Application.Abstractions;

using Microsoft.Extensions.Caching.Hybrid;

namespace Finmy.Budgeting.Application.Caching;

public class EnvelopeCacheInvalidator(HybridCache cache, IOutputCacheInvalidator outputCache) : IEnvelopeCacheInvalidator
{
    public async Task InvalidateAsync(DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc, CancellationToken cancellationToken)
    {
        var summaryTags = BudgetingCachePolicy.SummaryTagsForPeriod(periodStartUtc, periodEndUtc);

        await cache.RemoveByTagAsync([BudgetingCachePolicy.EnvelopeListTag, .. summaryTags], cancellationToken);
        await outputCache.EvictByTagsAsync([BudgetingCachePolicy.OutputListTag, BudgetingCachePolicy.OutputSummaryTag], cancellationToken);
    }
}
